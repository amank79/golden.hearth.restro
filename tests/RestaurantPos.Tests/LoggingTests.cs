namespace RestaurantPos.Tests;

public class LoggingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Start_up_and_api_calls_are_written_to_the_daily_log_file()
    {
        var client = factory.CreateClient();
        await client.GetAsync("/api/health");
        await client.GetAsync("/");

        var logFile = Assert.Single(Directory.GetFiles(Path.Combine(factory.DataRoot, "logs"), "app-*.log"));
        // The file is still open for writing, so read it with sharing allowed.
        using var reader = new StreamReader(new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        var text = await reader.ReadToEndAsync();

        Assert.Contains("Restaurant POS", text);
        Assert.Contains("GET /api/health responded 200", text);
        Assert.DoesNotContain("GET / responded", text);
    }
}
