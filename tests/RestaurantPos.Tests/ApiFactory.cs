using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantPos.Api.Features.Printing;

namespace RestaurantPos.Tests;

/// <summary>Runs the API against a throwaway data folder and SQLite file so tests never touch real data.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), $"pos-test-{Guid.NewGuid():N}");

    /// <summary>The API's clock. Tests can move it to check dates, financial years and "today".</summary>
    public TestClock Clock { get; } = new(TestClock.India(2026, 10, 11, 19, 30));

    /// <summary>Stands in for the Windows printers, so tests never print on a real one.</summary>
    public FakeBillPrinter Printer { get; } = new();

    /// <summary>Tests create their own menu unless a factory asks for the development sample menu.</summary>
    protected virtual bool SeedSampleMenu => false;

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Pos:DataRoot", DataRoot);
        builder.UseSetting("ConnectionStrings:Pos", $"Data Source={Path.Combine(DataRoot, "test.db")};Pooling=False");
        builder.UseSetting("SampleMenu:Seed", SeedSampleMenu ? "true" : "false");
        // The scheduled backup would race with tests; backup tests call DatabaseBackup directly.
        builder.UseSetting("Backup:Enabled", "false");
        builder.ConfigureServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            s.Replace(ServiceDescriptor.Singleton<IBillPrinter>(Printer));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(DataRoot)) Directory.Delete(DataRoot, recursive: true);
    }
}

/// <summary>Like <see cref="ApiFactory"/> but with the development sample menu loaded.</summary>
public class SampleMenuFactory : ApiFactory
{
    protected override bool SeedSampleMenu => true;
}

/// <summary>A pretend set of Windows printers that records what was sent to them.</summary>
public class FakeBillPrinter : IBillPrinter
{
    public List<string> Printers { get; } = ["Microsoft Print to PDF", "POS-80C"];
    public List<(string Printer, byte[] Data, string JobName)> Jobs { get; } = [];

    /// <summary>When set, sending fails with this message (printer unplugged, out of paper...).</summary>
    public string? Failure { get; set; }

    public IReadOnlyList<string> InstalledPrinters() => [.. Printers];

    public void Send(string printerName, byte[] data, string jobName)
    {
        if (Failure is not null) throw new PrinterException(Failure);
        if (!Printers.Contains(printerName)) throw new PrinterException($"Printer \"{printerName}\" was not found.");
        lock (Jobs) Jobs.Add((printerName, data, jobName));
    }
}
