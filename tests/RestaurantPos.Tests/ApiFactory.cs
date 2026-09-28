using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RestaurantPos.Tests;

/// <summary>Runs the API against a throwaway SQLite file so tests never touch the real database.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-test-{Guid.NewGuid():N}.db");

    /// <summary>The API's clock. Tests can move it to check dates, financial years and "today".</summary>
    public TestClock Clock { get; } = new(TestClock.India(2026, 10, 11, 19, 30));

    /// <summary>Tests create their own menu unless a factory asks for the development sample menu.</summary>
    protected virtual bool SeedSampleMenu => false;

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Pos", $"Data Source={_dbPath};Pooling=False");
        builder.UseSetting("SampleMenu:Seed", SeedSampleMenu ? "true" : "false");
        builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_dbPath);
    }
}

/// <summary>Like <see cref="ApiFactory"/> but with the development sample menu loaded.</summary>
public class SampleMenuFactory : ApiFactory
{
    protected override bool SeedSampleMenu => true;
}
