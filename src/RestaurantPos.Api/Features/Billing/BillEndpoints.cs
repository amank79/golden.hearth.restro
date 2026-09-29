using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Features.Billing;

/// <summary>Bills API: open bills, lines, discount, finalise, payments, cancel, history and today's total.</summary>
public static class BillEndpoints
{
    public const int MaxPageSize = 200;

    public static RouteGroupBuilder MapBillEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/bills");

        // Bills not paid or cancelled yet, oldest first (the tabs on the billing screen).
        g.MapGet("/open", async (PosDbContext db) =>
        {
            var bills = await SummaryQuery(db).Where(b => b.Status == BillStatus.Open).OrderBy(b => b.Id).ToListAsync();
            return bills.Select(ToSummary).ToList();
        });

        g.MapPost("", async (OpenBillInput input, BillService bills) =>
        {
            var bill = await bills.OpenAsync(input);
            return Results.Created($"/api/bills/{bill.Id}", ToDto(bill));
        });

        g.MapGet("/{id:int}", async (int id, BillService bills) => ToDto(await bills.LoadAsync(id)));

        g.MapPut("/{id:int}/table", async (int id, OpenBillInput input, BillService bills) => ToDto(await bills.ChangeTableAsync(id, input)));

        g.MapPost("/{id:int}/lines", async (int id, AddLineInput input, BillService bills) => ToDto(await bills.AddLineAsync(id, input)));

        g.MapPut("/{id:int}/lines/{lineId:int}", async (int id, int lineId, ChangeLineInput input, BillService bills) =>
            ToDto(await bills.ChangeLineAsync(id, lineId, input)));

        // Takes the line off the bill; the row is kept with a removed time.
        g.MapDelete("/{id:int}/lines/{lineId:int}", async (int id, int lineId, BillService bills) =>
            ToDto(await bills.RemoveLineAsync(id, lineId)));

        g.MapPut("/{id:int}/discount", async (int id, DiscountInput input, BillService bills) => ToDto(await bills.SetDiscountAsync(id, input)));

        g.MapPost("/{id:int}/finalise", async (int id, BillService bills) => ToDto(await bills.FinaliseAsync(id)));

        g.MapPost("/{id:int}/payments", async (int id, SettleInput input, BillService bills) => ToDto(await bills.SettleAsync(id, input)));

        g.MapPost("/{id:int}/cancel", async (int id, CancelInput input, BillService bills) =>
        {
            var (cancelled, copy) = await bills.CancelAsync(id, input);
            return new CancelResult(ToDto(cancelled), copy is null ? null : ToDto(copy));
        });

        // Bill history with search (bill number, table or dish) and India-date range, newest first.
        g.MapGet("", async (PosDbContext db, string? q, DateOnly? from, DateOnly? to, BillStatus? status, int skip = 0, int take = 50) =>
        {
            var query = SummaryQuery(db);
            if (status is not null) query = query.Where(b => b.Status == status);
            if (from is not null)
            {
                var start = IndiaTime.DayRange(from.Value).Start.ToUniversalTime();
                query = query.Where(b => (b.FinalisedAt ?? b.OpenedAt) >= start);
            }
            if (to is not null)
            {
                var end = IndiaTime.DayRange(to.Value).End.ToUniversalTime();
                query = query.Where(b => (b.FinalisedAt ?? b.OpenedAt) < end);
            }
            if (ApiErrors.Clean(q) is { } text)
            {
                var like = $"%{text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                var seq = int.TryParse(text, out var n) ? n : -1;
                query = query.Where(b =>
                    b.SeqNo == seq
                    || EF.Functions.Like(b.BillNo!, like, "\\")
                    || EF.Functions.Like(b.TableLabel!, like, "\\")
                    || b.Lines.Any(l => l.RemovedAt == null && EF.Functions.Like(l.ItemName, like, "\\")));
            }

            var total = await query.CountAsync();
            var page = await query.OrderByDescending(b => b.Id)
                .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, MaxPageSize)).ToListAsync();
            return new BillPage(page.Select(ToSummary).ToList(), total);
        });

        // Today's total (India date): bills finalised today, by payment method.
        g.MapGet("/today", async (PosDbContext db, TimeProvider clock) =>
        {
            var today = IndiaTime.Today(clock);
            var (start, end) = IndiaTime.DayRange(today);
            var (s, e) = (start.ToUniversalTime(), end.ToUniversalTime());
            var bills = await db.Bills.Include(b => b.Payments)
                .Where(b => b.FinalisedAt >= s && b.FinalisedAt < e)
                .ToListAsync();
            var counted = bills.Where(b => b.Status != BillStatus.Cancelled).ToList();
            var cancelled = bills.Where(b => b.Status == BillStatus.Cancelled).ToList();
            var byMethod = Enum.GetValues<PaymentMethod>()
                .Select(m => new MethodTotalDto(m, counted.SelectMany(b => b.Payments).Where(p => p.Method == m).Sum(p => p.AmountPaise)))
                .ToList();
            var openCount = await db.Bills.CountAsync(b => b.Status == BillStatus.Open);
            return new TodaySummaryDto(
                today,
                counted.Count,
                counted.Sum(b => b.TotalPaise),
                counted.Where(b => b.Status == BillStatus.Paid).Sum(b => b.TotalPaise),
                counted.Where(b => b.Status == BillStatus.Open).Sum(b => b.TotalPaise),
                byMethod,
                cancelled.Count,
                cancelled.Sum(b => b.TotalPaise),
                openCount);
        });

        return api;
    }

    private static IQueryable<Bill> SummaryQuery(PosDbContext db) =>
        db.Bills.Include(b => b.Lines.Where(l => l.RemovedAt == null)).Include(b => b.Payments).AsSplitQuery();

    public static BillSummaryDto ToSummary(Bill b) => new(
        b.Id, b.BillNo, b.OrderType, b.TableLabel, b.Status, b.OpenedAt, b.FinalisedAt, b.SettledAt,
        BillService.Active(b).Sum(l => l.Qty), b.TotalPaise,
        b.Payments.Select(p => p.Method).Distinct().Order().ToList(), b.CancelReason);

    public static BillDto ToDto(Bill b)
    {
        var t = BillService.Totals(b);
        var paid = b.Payments.Sum(p => p.AmountPaise);
        return new BillDto(
            b.Id, b.PublicId, b.BillNo, b.FinancialYear, b.OrderType, b.TableLabel, b.Status, b.BillNo is not null,
            b.OpenedAt, b.FinalisedAt, b.SettledAt, b.CancelledAt, b.CancelReason, b.TaxMode, t.DocumentTitle,
            BillService.Active(b).Select(l => new BillLineDto(
                l.Id, l.MenuItemId, l.ItemVariantId, l.ItemName, l.VariantName, l.UnitPricePaise, l.Qty, l.Note, l.GstRateBp,
                l.UnitPricePaise * l.Qty)).ToList(),
            b.DiscountKind, b.DiscountValue, b.DiscountReason,
            b.SubtotalPaise, b.DiscountPaise, b.TaxablePaise,
            t.TaxGroups.Select(g => new TaxGroupDto(g.GstRateBp, g.HalfRateBp, g.TaxablePaise, g.CgstPaise, g.SgstPaise)).ToList(),
            b.CgstPaise, b.SgstPaise, b.RoundOffPaise, b.TotalPaise,
            b.Payments.Select(p => new PaymentDto(p.Method, p.AmountPaise, p.At)).ToList(),
            paid, b.Status == BillStatus.Cancelled ? 0 : b.TotalPaise - paid, b.PrintCount);
    }
}
