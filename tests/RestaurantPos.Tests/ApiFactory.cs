using Microsoft.AspNetCore.Mvc.Testing;

namespace RestaurantPos.Tests;

/// <summary>Runs the API against a throwaway SQLite file so tests never touch the real database.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Pos", $"Data Source={_dbPath};Pooling=False");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_dbPath);
    }
}
