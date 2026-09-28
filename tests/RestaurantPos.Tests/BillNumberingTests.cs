using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;

namespace RestaurantPos.Tests;

public class FinancialYearTests
{
    [Theory]
    [InlineData(2026, 3, 31, 23, 59, 59, "2025-26")]
    [InlineData(2026, 4, 1, 0, 0, 0, "2026-27")]
    [InlineData(2026, 10, 11, 19, 30, 0, "2026-27")]
    [InlineData(2027, 1, 1, 0, 0, 0, "2026-27")]
    [InlineData(2027, 3, 31, 23, 59, 59, "2026-27")]
    [InlineData(2027, 4, 1, 0, 0, 0, "2027-28")]
    [InlineData(2099, 4, 1, 0, 0, 0, "2099-00")]
    public void Financial_year_in_india_time(int y, int mo, int d, int h, int mi, int s, string expected) =>
        Assert.Equal(expected, BillNumbering.FinancialYear(TestClock.India(y, mo, d, h, mi, s)));

    [Fact]
    public void Financial_year_changes_at_midnight_india_time_not_utc()
    {
        // 1 April 00:00 in India is 31 March 18:30 UTC.
        Assert.Equal("2025-26", BillNumbering.FinancialYear(new DateTimeOffset(2026, 3, 31, 18, 29, 59, TimeSpan.Zero)));
        Assert.Equal("2026-27", BillNumbering.FinancialYear(new DateTimeOffset(2026, 3, 31, 18, 30, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData("2026-27", 1, "2026-27/000001")]
    [InlineData("2026-27", 123, "2026-27/000123")]
    [InlineData("2026-27", 999999, "2026-27/999999")]
    [InlineData("2026-27", 1000000, "2026-27/1000000")]
    public void Format(string fy, int seq, string expected) => Assert.Equal(expected, BillNumbering.Format(fy, seq));

    [Fact]
    public void India_day_range()
    {
        var (start, end) = IndiaTime.DayRange(new DateOnly(2026, 10, 11));
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 18, 30, 0, TimeSpan.Zero), start.ToUniversalTime());
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 18, 30, 0, TimeSpan.Zero), end.ToUniversalTime());
        Assert.Equal(new DateOnly(2026, 10, 12), IndiaTime.DateOf(new DateTimeOffset(2026, 10, 11, 18, 30, 0, TimeSpan.Zero)));
    }
}

public class BillNumberingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<Bill> NewBill(PosDbContext db)
    {
        var bill = new Bill { OrderType = OrderType.Takeaway, OpenedAt = factory.Clock.GetUtcNow() };
        db.Bills.Add(bill);
        await db.SaveChangesAsync();
        return bill;
    }

    private static async Task Finalise(PosDbContext db, Bill bill, DateTimeOffset now)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await BillNumbering.AssignAsync(db, bill, now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    [Fact]
    public async Task Numbers_are_consecutive_only_for_finalised_bills_and_restart_on_1_april()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        // Late March of FY 2035-36 (a year no other test uses).
        var march31 = TestClock.India(2036, 3, 31, 23, 58);
        var a = await NewBill(db);
        var abandoned = await NewBill(db);
        var b = await NewBill(db);
        var c = await NewBill(db);

        await Finalise(db, b, march31);
        await Finalise(db, a, march31.AddSeconds(30));
        Assert.Equal("2035-36/000001", b.BillNo);
        Assert.Equal("2035-36/000002", a.BillNo);
        Assert.Equal(2, a.SeqNo);
        Assert.Equal("2035-36", a.FinancialYear);
        Assert.Equal(march31.AddSeconds(30), a.FinalisedAt);

        // A bill finalised after midnight India time starts the new financial year at 1.
        var april1 = TestClock.India(2036, 4, 1, 0, 0, 5);
        await Finalise(db, c, april1);
        Assert.Equal("2036-37/000001", c.BillNo);

        // The open bill that was never finalised used up no number.
        Assert.Null(abandoned.BillNo);
        Assert.Null(abandoned.SeqNo);
    }

    [Fact]
    public async Task Finalising_twice_keeps_the_same_number()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var bill = await NewBill(db);
        var when = TestClock.India(2040, 6, 1);
        await Finalise(db, bill, when);
        var first = bill.BillNo;
        await Finalise(db, bill, when.AddHours(1));
        Assert.Equal(first, bill.BillNo);
        Assert.Equal(when, bill.FinalisedAt);
    }

    [Fact]
    public async Task Number_must_be_assigned_inside_a_transaction()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var bill = await NewBill(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => BillNumbering.AssignAsync(db, bill, TestClock.India(2041, 6, 1)));
    }

    [Fact]
    public async Task Finalising_from_parallel_requests_gives_unique_consecutive_numbers()
    {
        var ids = new List<int>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            for (var i = 0; i < 20; i++) ids.Add((await NewBill(db)).Id);
        }

        var when = TestClock.India(2042, 5, 1);
        await Task.WhenAll(ids.Select(id => Task.Run(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var bill = await db.Bills.SingleAsync(b => b.Id == id);
            await Finalise(db, bill, when);
        })));

        using var check = factory.Services.CreateScope();
        var seqs = await check.ServiceProvider.GetRequiredService<PosDbContext>().Bills
            .Where(b => b.FinancialYear == "2042-43").Select(b => b.SeqNo!.Value).OrderBy(s => s).ToListAsync();
        Assert.Equal(Enumerable.Range(1, 20), seqs);
    }
}
