using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Features.Billing;

/// <summary>
/// Bill numbers (BILL-2): consecutive with no gaps within a financial year (1 April – 31 March, India time),
/// formatted "2026-27/000123". A number is given only when a bill is finalised (first print or payment), inside
/// the caller's database transaction, so open or abandoned orders never use one up. Cancelled bills keep their number.
/// </summary>
public static class BillNumbering
{
    /// <summary>"2026-27" for any time from 1 April 2026 00:00 to 31 March 2027 23:59:59 India time.</summary>
    public static string FinancialYear(DateTimeOffset time)
    {
        var local = IndiaTime.ToIndia(time);
        var startYear = local.Month >= 4 ? local.Year : local.Year - 1;
        return $"{startYear}-{(startYear + 1) % 100:00}";
    }

    public static string Format(string financialYear, int seqNo) => $"{financialYear}/{seqNo:000000}";

    /// <summary>
    /// Gives <paramref name="bill"/> the next number in the financial year of <paramref name="now"/> and marks it
    /// finalised. Must run inside a transaction (SQLite BEGIN IMMEDIATE holds the write lock, so no two bills can get
    /// the same number; the unique index on FinancialYear+SeqNo is the last safety net). Does nothing if the bill
    /// already has a number.
    /// </summary>
    public static async Task AssignAsync(PosDbContext db, Bill bill, DateTimeOffset now)
    {
        if (bill.BillNo is not null) return;
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Bill numbers must be assigned inside a database transaction.");
        }

        var fy = FinancialYear(now);
        var last = await db.Bills.Where(b => b.FinancialYear == fy).MaxAsync(b => b.SeqNo) ?? 0;
        bill.FinancialYear = fy;
        bill.SeqNo = last + 1;
        bill.BillNo = Format(fy, last + 1);
        bill.FinalisedAt = now;
    }
}
