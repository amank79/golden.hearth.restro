using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;
using RestaurantPos.Api.Features.Menu;
using RestaurantPos.Api.Features.Settings;

namespace RestaurantPos.Tests;

/// <summary>Helpers to drive bills through the API.</summary>
public static class BillApi
{
    public static async Task<BillDto> OpenDineIn(this HttpClient c, string table) =>
        await (await c.Post("/api/bills", new OpenBillInput(OrderType.DineIn, table))).Read<BillDto>();

    public static async Task<BillDto> OpenTakeaway(this HttpClient c) =>
        await (await c.Post("/api/bills", new OpenBillInput(OrderType.Takeaway, null))).Read<BillDto>();

    public static async Task<BillDto> AddLine(this HttpClient c, int billId, int variantId, int qty = 1, string? note = null) =>
        await (await c.Post($"/api/bills/{billId}/lines", new AddLineInput(variantId, qty, note))).Read<BillDto>();

    public static async Task<BillDto> Finalise(this HttpClient c, int billId) =>
        await (await c.PostAsync($"/api/bills/{billId}/finalise", null)).Read<BillDto>();

    public static async Task<BillDto> Pay(this HttpClient c, int billId, params PaymentInput[] payments) =>
        await (await c.Post($"/api/bills/{billId}/payments", new SettleInput(payments))).Read<BillDto>();

    public static async Task<string> ProblemTitle(this HttpResponseMessage r)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("title").GetString()!;
    }
}

/// <summary>A small menu shared by the bill tests: Paneer Tikka ₹280, Dal Makhani Half ₹150 / Full ₹240, Water ₹20 at 18%.</summary>
public class BillFixture : ApiFactory
{
    public MenuItemDto PaneerTikka { get; private set; } = null!;
    public MenuItemDto DalMakhani { get; private set; } = null!;
    public MenuItemDto Water { get; private set; } = null!;
    public MenuItemDto Rasmalai { get; private set; } = null!;

    public int Tikka => PaneerTikka.Variants[0].Id;
    public int DalHalf => DalMakhani.Variants[0].Id;
    public int DalFull => DalMakhani.Variants[1].Id;

    private bool _ready;

    public async Task EnsureMenu()
    {
        if (_ready) return;
        _ready = true;
        var c = CreateClient();
        var cat = await c.AddCategory("Bill test dishes");
        PaneerTikka = await c.AddItem(MenuApi.Dish(cat.Id, "Paneer Tikka", 280, "PT"));
        DalMakhani = await c.AddItem(MenuApi.HalfFull(cat.Id, "Dal Makhani", 150, 240, "DM"));
        Water = await c.AddItem(MenuApi.Dish(cat.Id, "Packaged Water", 20, "PW") with { GstRateBp = 1800 });
        Rasmalai = await c.AddItem(MenuApi.Dish(cat.Id, "Rasmalai", 110, "RM") with { IsAvailable = false });
    }
}

public class BillsApiTests(BillFixture f) : IClassFixture<BillFixture>, IAsyncLifetime
{
    private readonly HttpClient _c = f.CreateClient();

    public Task InitializeAsync() => f.EnsureMenu();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Open_dine_in_and_takeaway_bills()
    {
        var dine = await _c.OpenDineIn(" 4A ");
        Assert.Equal(OrderType.DineIn, dine.OrderType);
        Assert.Equal("4A", dine.TableLabel);
        Assert.Equal(BillStatus.Open, dine.Status);
        Assert.Null(dine.BillNo);
        Assert.False(dine.IsFinalised);

        var ta = await _c.OpenTakeaway();
        Assert.Null(ta.TableLabel);

        var open = await _c.Get<List<BillSummaryDto>>("/api/bills/open");
        Assert.Contains(open, b => b.Id == dine.Id);
        Assert.Contains(open, b => b.Id == ta.Id);
    }

    [Fact]
    public async Task Dine_in_needs_a_table_and_one_open_bill_per_table()
    {
        var noTable = await _c.Post("/api/bills", new OpenBillInput(OrderType.DineIn, " "));
        Assert.Equal(HttpStatusCode.BadRequest, noTable.StatusCode);

        await _c.OpenDineIn("T-dup");
        var again = await _c.Post("/api/bills", new OpenBillInput(OrderType.DineIn, "T-dup"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("already has an open bill", await again.ProblemTitle());
        Assert.Equal(HttpStatusCode.Conflict, (await _c.Post("/api/bills", new OpenBillInput(OrderType.DineIn, "t-DUP"))).StatusCode);
    }

    [Fact]
    public async Task Several_bills_can_be_open_at_once()
    {
        var a = await _c.OpenDineIn("Multi-1");
        var b = await _c.OpenDineIn("Multi-2");
        var t = await _c.OpenTakeaway();
        await _c.AddLine(a.Id, f.Tikka);
        await _c.AddLine(b.Id, f.DalFull, 2);
        await _c.AddLine(t.Id, f.DalHalf);

        Assert.Equal(28000, (await _c.Get<BillDto>($"/api/bills/{a.Id}")).SubtotalPaise);
        Assert.Equal(48000, (await _c.Get<BillDto>($"/api/bills/{b.Id}")).SubtotalPaise);
        Assert.Equal(15000, (await _c.Get<BillDto>($"/api/bills/{t.Id}")).SubtotalPaise);
    }

    [Fact]
    public async Task Add_lines_with_variant_qty_and_note_and_totals_update()
    {
        var bill = await _c.OpenDineIn("Lines-1");
        await _c.AddLine(bill.Id, f.Tikka);
        await _c.AddLine(bill.Id, f.DalFull, 2, "less spicy");
        bill = await _c.AddLine(bill.Id, f.Tikka); // same dish again -> same line, qty 2

        Assert.Equal(2, bill.Lines.Count);
        var tikka = bill.Lines.Single(l => l.ItemName == "Paneer Tikka");
        Assert.Equal(2, tikka.Qty);
        Assert.Equal("Regular", tikka.VariantName);
        var dal = bill.Lines.Single(l => l.ItemName == "Dal Makhani");
        Assert.Equal("Full", dal.VariantName);
        Assert.Equal(24000, dal.UnitPricePaise);
        Assert.Equal("less spicy", dal.Note);
        Assert.Equal(500, dal.GstRateBp);

        // 560 + 480 = 1040; 2.5% = 26.00 each; 1092.00
        Assert.Equal(104000, bill.SubtotalPaise);
        Assert.Equal(2600, bill.CgstPaise);
        Assert.Equal(2600, bill.SgstPaise);
        Assert.Equal(109200, bill.TotalPaise);
        Assert.Equal("Tax Invoice", bill.DocumentTitle);
    }

    [Fact]
    public async Task Same_dish_with_a_different_note_is_a_separate_line()
    {
        var bill = await _c.OpenDineIn("Lines-2");
        await _c.AddLine(bill.Id, f.Tikka);
        bill = await _c.AddLine(bill.Id, f.Tikka, 1, "no onion");
        Assert.Equal(2, bill.Lines.Count);
    }

    [Fact]
    public async Task Change_and_remove_lines()
    {
        var bill = await _c.OpenDineIn("Lines-3");
        bill = await _c.AddLine(bill.Id, f.Tikka);
        bill = await _c.AddLine(bill.Id, f.DalHalf);
        var tikka = bill.Lines.Single(l => l.ItemName == "Paneer Tikka");

        bill = await (await _c.Put($"/api/bills/{bill.Id}/lines/{tikka.Id}", new ChangeLineInput(3, "extra chutney"))).Read<BillDto>();
        Assert.Equal(3, bill.Lines.Single(l => l.Id == tikka.Id).Qty);
        Assert.Equal("extra chutney", bill.Lines.Single(l => l.Id == tikka.Id).Note);
        Assert.Equal(84000 + 15000, bill.SubtotalPaise);

        bill = await (await _c.DeleteAsync($"/api/bills/{bill.Id}/lines/{tikka.Id}")).Read<BillDto>();
        Assert.Single(bill.Lines);
        Assert.Equal(15000, bill.SubtotalPaise);

        // The removed line is kept in the database.
        using var scope = f.Services.CreateScope();
        var row = scope.ServiceProvider.GetRequiredService<PosDbContext>().BillLines.Single(l => l.Id == tikka.Id);
        Assert.NotNull(row.RemovedAt);

        // Adding the same dish again makes a new line.
        bill = await _c.AddLine(bill.Id, f.Tikka);
        Assert.Equal(2, bill.Lines.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task Quantity_must_be_1_to_999(int qty)
    {
        var bill = await _c.OpenTakeaway();
        var r = await _c.Post($"/api/bills/{bill.Id}/lines", new AddLineInput(f.Tikka, qty, null));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Unavailable_or_removed_dishes_cannot_be_added()
    {
        var bill = await _c.OpenTakeaway();
        var r = await _c.Post($"/api/bills/{bill.Id}/lines", new AddLineInput(f.Rasmalai.Variants[0].Id, 1, null));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("not available", await r.ProblemTitle());

        var r2 = await _c.Post($"/api/bills/{bill.Id}/lines", new AddLineInput(999999, 1, null));
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
    }

    [Fact]
    public async Task Menu_price_change_does_not_change_existing_lines()
    {
        var cat = await _c.AddCategory("Price change");
        var dish = await _c.AddItem(MenuApi.Dish(cat.Id, "Veg Thali", 200));
        var bill = await _c.OpenDineIn("Price-1");
        await _c.AddLine(bill.Id, dish.Variants[0].Id);

        var edit = MenuApi.Dish(cat.Id, "Veg Thali Deluxe", 0) with { Variants = [new VariantInput(dish.Variants[0].Id, null, 25000)] };
        await _c.Put($"/api/menu/items/{dish.Id}", edit);

        var after = await _c.Get<BillDto>($"/api/bills/{bill.Id}");
        Assert.Equal("Veg Thali", after.Lines[0].ItemName);
        Assert.Equal(20000, after.Lines[0].UnitPricePaise);

        // The new price is used for the next line.
        after = await _c.AddLine(bill.Id, dish.Variants[0].Id);
        Assert.Equal([20000L, 25000L], after.Lines.Select(l => l.UnitPricePaise));
    }

    [Fact]
    public async Task Percent_and_amount_discount_with_reason()
    {
        var bill = await _c.OpenDineIn("Disc-1");
        await _c.AddLine(bill.Id, f.DalFull, 5); // 1200

        var noReason = await _c.Put($"/api/bills/{bill.Id}/discount", new DiscountInput(DiscountKind.Percent, 1000, null));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        bill = await (await _c.Put($"/api/bills/{bill.Id}/discount", new DiscountInput(DiscountKind.Percent, 1000, "Family friend"))).Read<BillDto>();
        Assert.Equal(12000, bill.DiscountPaise);
        Assert.Equal(108000, bill.TaxablePaise);
        Assert.Equal("Family friend", bill.DiscountReason);

        // Percent discount follows later changes to the items.
        bill = await _c.AddLine(bill.Id, f.Tikka); // +280 = 1480
        Assert.Equal(14800, bill.DiscountPaise);

        bill = await (await _c.Put($"/api/bills/{bill.Id}/discount", new DiscountInput(DiscountKind.Amount, 5000, "Regular customer"))).Read<BillDto>();
        Assert.Equal(5000, bill.DiscountPaise);
        Assert.Equal(DiscountKind.Amount, bill.DiscountKind);

        var tooMuch = await _c.Put($"/api/bills/{bill.Id}/discount", new DiscountInput(DiscountKind.Amount, 200000, "x"));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        var badPercent = await _c.Put($"/api/bills/{bill.Id}/discount", new DiscountInput(DiscountKind.Percent, 10001, "x"));
        Assert.Equal(HttpStatusCode.BadRequest, badPercent.StatusCode);

        bill = await (await _c.Put($"/api/bills/{bill.Id}/discount", new DiscountInput(DiscountKind.None, 0, null))).Read<BillDto>();
        Assert.Equal(0, bill.DiscountPaise);
        Assert.Null(bill.DiscountReason);
    }

    [Fact]
    public async Task Mixed_gst_rates_are_shown_per_rate()
    {
        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.Tikka);
        bill = await _c.AddLine(bill.Id, f.Water.Variants[0].Id);
        Assert.Equal([500, 1800], bill.TaxGroups.Select(g => g.GstRateBp));
        Assert.Equal(700 + 180, bill.CgstPaise); // 280 × 2.5% = 7.00; 20 × 9% = 1.80
    }

    [Fact]
    public async Task Finalise_assigns_a_number_and_locks_the_bill()
    {
        var bill = await _c.OpenDineIn("Fin-1");
        await _c.AddLine(bill.Id, f.Tikka);
        bill = await _c.Finalise(bill.Id);

        Assert.True(bill.IsFinalised);
        Assert.Matches(@"^2026-27/\d{6}$", bill.BillNo);
        Assert.Equal(BillStatus.Open, bill.Status);
        Assert.Equal(f.Clock.GetUtcNow(), bill.FinalisedAt);

        // Finalising again keeps the number.
        Assert.Equal(bill.BillNo, (await _c.Finalise(bill.Id)).BillNo);

        var add = await _c.Post($"/api/bills/{bill.Id}/lines", new AddLineInput(f.Tikka, 1, null));
        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
        Assert.Contains("already printed", await add.ProblemTitle());
        var disc = await _c.Put($"/api/bills/{bill.Id}/discount", new DiscountInput(DiscountKind.Percent, 500, "x"));
        Assert.Equal(HttpStatusCode.Conflict, disc.StatusCode);
    }

    [Fact]
    public async Task Empty_bill_cannot_be_finalised()
    {
        var bill = await _c.OpenTakeaway();
        var r = await _c.PostAsync($"/api/bills/{bill.Id}/finalise", null);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task Bill_numbers_are_consecutive_and_skip_open_bills()
    {
        var a = await _c.OpenTakeaway();
        var abandoned = await _c.OpenTakeaway();
        var b = await _c.OpenTakeaway();
        await _c.AddLine(a.Id, f.Tikka);
        await _c.AddLine(abandoned.Id, f.Tikka);
        await _c.AddLine(b.Id, f.Tikka);

        var first = await _c.Finalise(b.Id);
        var second = await _c.Finalise(a.Id);
        Assert.Equal(first.BillNo![..8], second.BillNo![..8]);
        Assert.Equal(int.Parse(first.BillNo[8..]) + 1, int.Parse(second.BillNo[8..]));

        // Cancelling the abandoned order never gives it a number.
        await _c.Post($"/api/bills/{abandoned.Id}/cancel", new CancelInput("Customer left"));
        Assert.Null((await _c.Get<BillDto>($"/api/bills/{abandoned.Id}")).BillNo);
    }

    [Fact]
    public async Task Pay_by_cash_finalises_and_closes_the_bill()
    {
        var bill = await _c.OpenDineIn("Pay-1");
        bill = await _c.AddLine(bill.Id, f.Tikka); // 280 + 14 = 294
        Assert.Equal(29400, bill.TotalPaise);

        bill = await _c.Pay(bill.Id, new PaymentInput(PaymentMethod.Cash, 29400));
        Assert.Equal(BillStatus.Paid, bill.Status);
        Assert.NotNull(bill.BillNo);
        Assert.NotNull(bill.SettledAt);
        Assert.Equal(29400, bill.PaidPaise);
        Assert.Equal(0, bill.BalancePaise);
        Assert.DoesNotContain(await _c.Get<List<BillSummaryDto>>("/api/bills/open"), b => b.Id == bill.Id);

        // The table is free again.
        await _c.OpenDineIn("Pay-1");
    }

    [Fact]
    public async Task Split_payment()
    {
        var bill = await _c.OpenTakeaway();
        bill = await _c.AddLine(bill.Id, f.DalFull, 5); // 1200 + 60 = 1260
        bill = await _c.Pay(bill.Id,
            new PaymentInput(PaymentMethod.Cash, 50000),
            new PaymentInput(PaymentMethod.Upi, 60000),
            new PaymentInput(PaymentMethod.Card, 16000));
        Assert.Equal(3, bill.Payments.Count);
        Assert.Equal(126000, bill.PaidPaise);
    }

    [Fact]
    public async Task Payments_must_add_up_to_the_total()
    {
        var bill = await _c.OpenTakeaway();
        bill = await _c.AddLine(bill.Id, f.Tikka);
        var r = await _c.Post($"/api/bills/{bill.Id}/payments", new SettleInput([new PaymentInput(PaymentMethod.Cash, 29300)]));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var zero = await _c.Post($"/api/bills/{bill.Id}/payments", new SettleInput([new PaymentInput(PaymentMethod.Cash, 29400), new PaymentInput(PaymentMethod.Upi, 0)]));
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);

        // A failed payment must not use up a bill number either (it rolled back).
        Assert.Null((await _c.Get<BillDto>($"/api/bills/{bill.Id}")).BillNo);

        await _c.Pay(bill.Id, new PaymentInput(PaymentMethod.Upi, 29400));
        var twice = await _c.Post($"/api/bills/{bill.Id}/payments", new SettleInput([new PaymentInput(PaymentMethod.Cash, 29400)]));
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
    }

    [Fact]
    public async Task Cancel_keeps_the_bill_with_reason_and_time()
    {
        var bill = await _c.OpenDineIn("Cancel-1");
        await _c.AddLine(bill.Id, f.Tikka);
        bill = await _c.Finalise(bill.Id);

        var noReason = await _c.Post($"/api/bills/{bill.Id}/cancel", new CancelInput(" "));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var result = await (await _c.Post($"/api/bills/{bill.Id}/cancel", new CancelInput("Wrong table billed"))).Read<CancelResult>();
        Assert.Equal(BillStatus.Cancelled, result.Cancelled.Status);
        Assert.Equal("Wrong table billed", result.Cancelled.CancelReason);
        Assert.Equal(f.Clock.GetUtcNow(), result.Cancelled.CancelledAt);
        Assert.Equal(bill.BillNo, result.Cancelled.BillNo); // keeps its number: no gap
        Assert.Null(result.NewBill);

        // Still there, in history.
        var fetched = await _c.Get<BillDto>($"/api/bills/{bill.Id}");
        Assert.Equal(BillStatus.Cancelled, fetched.Status);
        Assert.Single(fetched.Lines);

        var again = await _c.Post($"/api/bills/{bill.Id}/cancel", new CancelInput("again"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var pay = await _c.Post($"/api/bills/{bill.Id}/payments", new SettleInput([new PaymentInput(PaymentMethod.Cash, 29400)]));
        Assert.Equal(HttpStatusCode.Conflict, pay.StatusCode);
    }

    [Fact]
    public async Task Cancel_and_copy_to_a_new_bill()
    {
        var bill = await _c.OpenDineIn("Cancel-2");
        await _c.AddLine(bill.Id, f.Tikka, 2, "no onion");
        await _c.AddLine(bill.Id, f.DalHalf);
        bill = await _c.Finalise(bill.Id);

        var result = await (await _c.Post($"/api/bills/{bill.Id}/cancel", new CancelInput("Customer added dishes", true))).Read<CancelResult>();
        var copy = result.NewBill!;
        Assert.Equal(BillStatus.Open, copy.Status);
        Assert.Null(copy.BillNo);
        Assert.Equal("Cancel-2", copy.TableLabel);
        Assert.Equal(bill.Lines.Select(l => (l.ItemName, l.Qty, l.Note, l.UnitPricePaise)), copy.Lines.Select(l => (l.ItemName, l.Qty, l.Note, l.UnitPricePaise)));
        Assert.Equal(bill.TotalPaise, copy.TotalPaise);

        // The copy is editable.
        await _c.AddLine(copy.Id, f.Tikka);
    }

    [Fact]
    public async Task Paid_bill_can_be_cancelled_and_leaves_today_total()
    {
        var before = await _c.Get<TodaySummaryDto>("/api/bills/today");
        var bill = await _c.OpenTakeaway();
        bill = await _c.AddLine(bill.Id, f.Tikka);
        await _c.Pay(bill.Id, new PaymentInput(PaymentMethod.Card, bill.TotalPaise));

        var paid = await _c.Get<TodaySummaryDto>("/api/bills/today");
        Assert.Equal(before.TotalPaise + 29400, paid.TotalPaise);

        await _c.Post($"/api/bills/{bill.Id}/cancel", new CancelInput("Refunded, food was cold"));
        var after = await _c.Get<TodaySummaryDto>("/api/bills/today");
        Assert.Equal(before.TotalPaise, after.TotalPaise);
        Assert.Equal(before.CancelledCount + 1, after.CancelledCount);
    }

    [Fact]
    public async Task Change_table_before_printing()
    {
        var bill = await _c.OpenDineIn("Move-1");
        bill = await (await _c.Put($"/api/bills/{bill.Id}/table", new OpenBillInput(OrderType.DineIn, "Move-2"))).Read<BillDto>();
        Assert.Equal("Move-2", bill.TableLabel);
        bill = await (await _c.Put($"/api/bills/{bill.Id}/table", new OpenBillInput(OrderType.Takeaway, "ignored"))).Read<BillDto>();
        Assert.Equal(OrderType.Takeaway, bill.OrderType);
        Assert.Null(bill.TableLabel);
    }

    [Fact]
    public async Task History_search_by_number_table_and_dish()
    {
        var bill = await _c.OpenDineIn("Hist-77");
        await _c.AddLine(bill.Id, f.Water.Variants[0].Id);
        bill = await _c.Finalise(bill.Id);
        var seq = int.Parse(bill.BillNo![8..]);

        var bySeq = await _c.Get<BillPage>($"/api/bills?q={seq}");
        Assert.Contains(bySeq.Items, b => b.Id == bill.Id);
        var byFull = await _c.Get<BillPage>($"/api/bills?q={Uri.EscapeDataString(bill.BillNo)}");
        Assert.Equal(bill.Id, Assert.Single(byFull.Items).Id);
        var byTable = await _c.Get<BillPage>("/api/bills?q=hist-77");
        Assert.Equal(bill.Id, Assert.Single(byTable.Items).Id);
        var byDish = await _c.Get<BillPage>("/api/bills?q=packaged");
        Assert.Contains(byDish.Items, b => b.Id == bill.Id);
        var none = await _c.Get<BillPage>("/api/bills?q=zzz-nothing");
        Assert.Empty(none.Items);
        Assert.Equal(0, none.Total);
    }

    [Fact]
    public async Task History_filters_by_status_and_date_and_pages()
    {
        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.Tikka);
        await _c.Post($"/api/bills/{bill.Id}/cancel", new CancelInput("test"));

        var cancelled = await _c.Get<BillPage>("/api/bills?status=Cancelled&take=200");
        Assert.All(cancelled.Items, b => Assert.Equal(BillStatus.Cancelled, b.Status));
        Assert.Contains(cancelled.Items, b => b.Id == bill.Id && b.CancelReason == "test");

        var today = IndiaTime.Today(f.Clock).ToString("yyyy-MM-dd");
        var byDate = await _c.Get<BillPage>($"/api/bills?from={today}&to={today}&take=200");
        Assert.Contains(byDate.Items, b => b.Id == bill.Id);
        var otherDay = await _c.Get<BillPage>("/api/bills?from=2020-01-01&to=2020-01-02");
        Assert.Empty(otherDay.Items);

        var page = await _c.Get<BillPage>("/api/bills?take=2");
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.Total >= 2);
        Assert.True(page.Items[0].Id > page.Items[1].Id); // newest first
    }

    [Fact]
    public async Task Composition_mode_bill_has_no_tax_and_is_frozen_when_finalised()
    {
        var settings = await _c.Get<SettingsDto>("/api/settings");
        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.Tikka);
        try
        {
            await _c.Put("/api/settings", settings with { TaxMode = TaxMode.Composition });
            bill = await _c.Get<BillDto>($"/api/bills/{bill.Id}");
            Assert.Equal(TaxMode.Composition, bill.TaxMode); // open bills follow the setting
            Assert.Equal("Bill of Supply", bill.DocumentTitle);
            Assert.Equal(0, bill.CgstPaise);
            Assert.Equal(28000, bill.TotalPaise);
            bill = await _c.Finalise(bill.Id);
        }
        finally
        {
            await _c.Put("/api/settings", settings);
        }

        // Switching back does not change the issued bill.
        var after = await _c.Get<BillDto>($"/api/bills/{bill.Id}");
        Assert.Equal(TaxMode.Composition, after.TaxMode);
        Assert.Equal(28000, after.TotalPaise);
    }

    [Fact]
    public async Task Unknown_bill_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _c.GetAsync("/api/bills/999999")).StatusCode);
    }
}

/// <summary>Today's total with its own database and clock.</summary>
public class TodayTotalTests(BillFixture f) : IClassFixture<BillFixture>, IAsyncLifetime
{
    private readonly HttpClient _c = f.CreateClient();

    public Task InitializeAsync() => f.EnsureMenu();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Todays_total_by_payment_method_in_india_time()
    {
        // Yesterday evening (India): one paid bill, not counted today.
        f.Clock.Now = TestClock.India(2026, 10, 11, 23, 50);
        var y = await _c.OpenTakeaway();
        y = await _c.AddLine(y.Id, f.Tikka);
        await _c.Pay(y.Id, new PaymentInput(PaymentMethod.Cash, y.TotalPaise));

        // Today (just after midnight India time = still 11 Oct in UTC).
        f.Clock.Now = TestClock.India(2026, 10, 12, 0, 10);
        var a = await _c.OpenTakeaway();
        a = await _c.AddLine(a.Id, f.Tikka); // 294
        await _c.Pay(a.Id, new PaymentInput(PaymentMethod.Cash, 10000), new PaymentInput(PaymentMethod.Upi, 19400));
        var b = await _c.OpenDineIn("Today-1");
        b = await _c.AddLine(b.Id, f.DalFull); // 252
        await _c.Finalise(b.Id); // printed, not paid yet
        var c = await _c.OpenTakeaway();
        c = await _c.AddLine(c.Id, f.DalHalf);
        await _c.Pay(c.Id, new PaymentInput(PaymentMethod.Card, c.TotalPaise));
        await _c.Post($"/api/bills/{c.Id}/cancel", new CancelInput("Mistake"));
        await _c.OpenDineIn("Today-2"); // open, empty

        var t = await _c.Get<TodaySummaryDto>("/api/bills/today");
        Assert.Equal(new DateOnly(2026, 10, 12), t.Date);
        Assert.Equal(2, t.BillCount);
        Assert.Equal(29400 + 25200, t.TotalPaise);
        Assert.Equal(29400, t.PaidPaise);
        Assert.Equal(25200, t.UnpaidPaise);
        Assert.Equal(10000, t.ByMethod.Single(m => m.Method == PaymentMethod.Cash).AmountPaise);
        Assert.Equal(19400, t.ByMethod.Single(m => m.Method == PaymentMethod.Upi).AmountPaise);
        Assert.Equal(0, t.ByMethod.Single(m => m.Method == PaymentMethod.Card).AmountPaise);
        Assert.Equal(1, t.CancelledCount);
        Assert.Equal(2, t.OpenCount);
    }
}
