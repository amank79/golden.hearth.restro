using System.Net;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Settings;

namespace RestaurantPos.Tests;

public class SettingsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static SettingsDto Valid() => new(
        "Golden Hearth", "Main Road, Ahmedabad", "98250 00000", "24abcde1234f1z5", "12345678901234",
        TaxMode.Regular, 500, "Thank you! Visit again.");

    [Fact]
    public async Task Get_returns_defaults()
    {
        var s = await factory.CreateClient().Get<SettingsDto>("/api/settings");
        Assert.Equal(TaxMode.Regular, s.TaxMode);
        Assert.Equal(500, s.GstRateBp);
    }

    [Fact]
    public async Task Put_saves_and_normalises()
    {
        var client = factory.CreateClient();
        var saved = await (await client.Put("/api/settings", Valid() with { Name = "  Golden Hearth  " })).Read<SettingsDto>();
        Assert.Equal("Golden Hearth", saved.Name);
        Assert.Equal("24ABCDE1234F1Z5", saved.Gstin);

        var again = await client.Get<SettingsDto>("/api/settings");
        Assert.Equal(saved, again);
    }

    [Fact]
    public async Task Composition_mode_is_saved()
    {
        var client = factory.CreateClient();
        var saved = await (await client.Put("/api/settings", Valid() with { TaxMode = TaxMode.Composition })).Read<SettingsDto>();
        Assert.Equal(TaxMode.Composition, saved.TaxMode);
        await client.Put("/api/settings", Valid());
    }

    [Theory]
    [InlineData("name", "", "", "", 500)]
    [InlineData("gstin", "Golden", "24ABCDE1234", "", 500)]
    [InlineData("fssaiNo", "Golden", "", "1234", 500)]
    [InlineData("gstRateBp", "Golden", "", "", 2900)]
    [InlineData("gstRateBp", "Golden", "", "", -1)]
    [InlineData("gstRateBp", "Golden", "", "", 501)]
    public async Task Put_rejects_invalid_values(string field, string name, string gstin, string fssai, int rate)
    {
        var response = await factory.CreateClient().Put("/api/settings",
            Valid() with { Name = name, Gstin = gstin, FssaiNo = fssai, GstRateBp = rate });
        Assert.Contains(field, await response.ErrorFields());
    }

    [Fact]
    public async Task Owner_pin_hash_is_never_returned()
    {
        var json = await (await factory.CreateClient().GetAsync("/api/settings")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("pin", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_the_app_page()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
