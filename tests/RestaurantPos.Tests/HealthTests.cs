using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Tests;

public class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Database_is_created_with_default_settings()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var settings = Assert.Single(db.Settings.ToList());
        Assert.Equal(500, settings.GstRateBp);
    }
}
