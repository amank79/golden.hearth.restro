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
        Assert.Equal(saved with { Tables = null }, again with { Tables = null });
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

public class TableListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Tables_start_as_1_to_20_and_can_be_changed()
    {
        var client = factory.CreateClient();
        var start = await client.Get<SettingsDto>("/api/settings");
        Assert.Equal(Enumerable.Range(1, 20).Select(n => n.ToString()), start.Tables);

        var saved = await (await client.Put("/api/settings/tables", new TablesInput([" 1 ", "2", "Garden 1", "AC Hall"]))).Read<SettingsDto>();
        Assert.Equal(["1", "2", "Garden 1", "AC Hall"], saved.Tables);

        // Saving the restaurant details does not touch the tables.
        await client.Put("/api/settings", start with { Tables = null });
        Assert.Equal(["1", "2", "Garden 1", "AC Hall"], (await client.Get<SettingsDto>("/api/settings")).Tables);

        var none = await (await client.Put("/api/settings/tables", new TablesInput([]))).Read<SettingsDto>();
        Assert.Empty(none.Tables!);

        await client.Put("/api/settings/tables", new TablesInput(start.Tables));
    }

    [Theory]
    [InlineData("5", "5")]
    [InlineData("Garden", "garden")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A table name that is too long")]
    public async Task Rejects_empty_long_and_duplicate_tables(params string[] tables)
    {
        var response = await factory.CreateClient().Put("/api/settings/tables", new TablesInput(tables));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tables", await response.ErrorFields());
    }

    [Fact]
    public async Task Rejects_more_than_100_tables()
    {
        var tables = Enumerable.Range(1, SettingsEndpoints.MaxTables + 1).Select(n => n.ToString()).ToList();
        var response = await factory.CreateClient().Put("/api/settings/tables", new TablesInput(tables));
        Assert.Contains("tables", await response.ErrorFields());
    }
}
