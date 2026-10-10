using System.Net;
using System.Text.Json;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;
using RestaurantPos.Api.Features.Menu;
using RestaurantPos.Api.Features.Owner;
using RestaurantPos.Api.Features.Settings;

namespace RestaurantPos.Tests;

public class OwnerPinHashTests
{
    [Fact]
    public void Hash_hides_the_pin_and_verifies_it()
    {
        var stored = OwnerPin.Hash("4821");
        Assert.DoesNotContain("4821", stored);
        Assert.NotEqual(stored, OwnerPin.Hash("4821"));
        Assert.True(OwnerPin.Verify("4821", stored));
        Assert.False(OwnerPin.Verify("4822", stored));
    }

    [Theory]
    [InlineData("1234", true)]
    [InlineData("123456", true)]
    [InlineData("123", false)]
    [InlineData("1234567", false)]
    [InlineData("12a4", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Pin_must_be_4_to_6_digits(string? pin, bool ok) => Assert.Equal(ok, OwnerPin.IsValidFormat(pin));
}

/// <summary>Each test gets its own app, so a PIN set in one test never leaks into another.</summary>
public sealed class OwnerPinTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public OwnerPinTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<string> SetPin(string pin = "4821") =>
        (await (await _client.Put("/api/owner/pin", new PinInput(pin))).Read<UnlockResult>()).Token;

    private HttpClient WithToken(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add(OwnerPin.TokenHeader, token);
        return c;
    }

    private static async Task AssertPinNeeded(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(OwnerPin.RequiredCode, doc.RootElement.GetProperty("code").GetString());
    }

    private static CategoryInput Category(string name) => new(name, null);

    [Fact]
    public async Task Without_a_pin_everything_works_as_before()
    {
        Assert.False((await _client.Get<OwnerStatus>("/api/owner/status")).PinSet);
        Assert.Equal(HttpStatusCode.Created, (await _client.Post("/api/menu/categories", Category("Starters"))).StatusCode);
    }

    [Fact]
    public async Task Protected_actions_need_the_pin_but_billing_and_the_available_switch_do_not()
    {
        var setup = await SetPin();
        var owner = WithToken(setup);
        var category = await (await owner.Post("/api/menu/categories", Category("Paneer"))).Read<CategoryDto>();
        var dish = await (await owner.Post("/api/menu/items", new MenuItemInput(
            category.Id, "Paneer Tikka", "PT", null, FoodType.Veg, null, true, null, [new VariantInput(null, null, 28000)]))).Read<MenuItemDto>();
        await owner.PostAsync("/api/owner/lock", null);

        // Locked again: the token no longer works.
        await AssertPinNeeded(await owner.Post("/api/menu/categories", Category("Breads")));
        await AssertPinNeeded(await _client.Post("/api/menu/categories", Category("Breads")));
        await AssertPinNeeded(await _client.Put($"/api/menu/items/{dish.Id}/active", new ActiveInput(false)));
        await AssertPinNeeded(await _client.Put("/api/settings/tables", new TablesInput(["1"])));

        // Billing without the PIN.
        Assert.Equal(HttpStatusCode.OK, (await _client.Put($"/api/menu/items/{dish.Id}/availability", new AvailabilityInput(false))).StatusCode);
        await _client.Put($"/api/menu/items/{dish.Id}/availability", new AvailabilityInput(true));
        var bill = await (await _client.Post("/api/bills", new { orderType = "Takeaway" })).Read<JsonElement>();
        var billId = bill.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await _client.Post($"/api/bills/{billId}/lines", new AddLineInput(dish.Variants[0].Id, 1, null))).StatusCode);

        await AssertPinNeeded(await _client.Put($"/api/bills/{billId}/discount", new { kind = "Percent", value = 1000, reason = "Friend" }));
        await AssertPinNeeded(await _client.Post($"/api/bills/{billId}/cancel", new { reason = "Customer left" }));

        var unlocked = await (await _client.Post("/api/owner/unlock", new PinInput("4821"))).Read<UnlockResult>();
        var cancel = await WithToken(unlocked.Token).Post($"/api/bills/{billId}/cancel", new { reason = "Customer left" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.True(cancel.Headers.Contains(OwnerPin.ExpiresHeader));
    }

    [Fact]
    public async Task Wrong_pin_is_refused_and_five_wrong_tries_wait_a_minute()
    {
        await SetPin();
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.Post("/api/owner/unlock", new PinInput("1111"))).StatusCode);
        for (var i = 0; i < OwnerSessions.MaxWrongTries - 1; i++) await _client.Post("/api/owner/unlock", new PinInput("1111"));

        // Blocked even with the right PIN.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await _client.Post("/api/owner/unlock", new PinInput("4821"))).StatusCode);

        _factory.Clock.Advance(OwnerSessions.WaitAfterWrongTries + TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.OK, (await _client.Post("/api/owner/unlock", new PinInput("4821"))).StatusCode);
    }

    [Fact]
    public async Task Unlock_lasts_five_minutes_after_the_last_protected_action()
    {
        var owner = WithToken(await SetPin());
        _factory.Clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(HttpStatusCode.Created, (await owner.Post("/api/menu/categories", Category("Paneer"))).StatusCode);
        _factory.Clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(HttpStatusCode.Created, (await owner.Post("/api/menu/categories", Category("Veg"))).StatusCode);
        Assert.True((await owner.Get<OwnerStatus>("/api/owner/status")).Unlocked);

        _factory.Clock.Advance(TimeSpan.FromMinutes(6));
        await AssertPinNeeded(await owner.Post("/api/menu/categories", Category("Dal")));
        Assert.False((await owner.Get<OwnerStatus>("/api/owner/status")).Unlocked);
    }

    [Fact]
    public async Task Changing_or_removing_the_pin_needs_the_pin()
    {
        var first = await SetPin("4821");
        await AssertPinNeeded(await _client.Put("/api/owner/pin", new PinInput("9999")));
        await AssertPinNeeded(await _client.DeleteAsync("/api/owner/pin"));

        var changed = await (await WithToken(first).Put("/api/owner/pin", new PinInput("135790"))).Read<UnlockResult>();
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.Post("/api/owner/unlock", new PinInput("4821"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.Post("/api/owner/unlock", new PinInput("135790"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await WithToken(changed.Token).DeleteAsync("/api/owner/pin")).StatusCode);
        Assert.False((await _client.Get<OwnerStatus>("/api/owner/status")).PinSet);
        Assert.Equal(HttpStatusCode.Created, (await _client.Post("/api/menu/categories", Category("Starters"))).StatusCode);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcd")]
    [InlineData("1234567")]
    public async Task Pin_format_is_checked(string pin)
    {
        Assert.Contains("pin", await (await _client.Put("/api/owner/pin", new PinInput(pin))).ErrorFields());
    }

    [Fact]
    public async Task Settings_never_show_the_pin_hash()
    {
        await SetPin();
        var json = await (await _client.GetAsync("/api/settings")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("v1$", json);
        Assert.DoesNotContain("pin", json, StringComparison.OrdinalIgnoreCase);
    }
}
