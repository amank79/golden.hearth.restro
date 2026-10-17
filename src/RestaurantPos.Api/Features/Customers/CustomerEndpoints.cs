using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;

namespace RestaurantPos.Api.Features.Customers;

/// <param name="Visits">Bills for this customer that were printed or paid and not cancelled.</param>
/// <param name="SpentPaise">Total of those bills.</param>
public record CustomerDto(int Id, string Name, string Phone, int DiscountBp, int Visits, long SpentPaise, DateTimeOffset? LastVisitAt);

public record CustomerInput(string? Name, string? Phone, int DiscountBp);

/// <summary>
/// Customers (regulars) found by phone or name. The phone number is personal data: search sends it in the query
/// string, which the request log leaves out, and it is never logged or printed.
/// </summary>
public static class CustomerEndpoints
{
    public const int MaxName = 60;
    public const int MaxResults = 50;

    public static RouteGroupBuilder MapCustomerEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/customers");

        // Search by phone digits or name; without a search, the most recent customers first.
        g.MapGet("", async (PosDbContext db, string? q, int take = 20) =>
        {
            var query = db.Customers.AsQueryable();
            if (ApiErrors.Clean(q) is { } text)
            {
                // Only digits (spaces, + and - allowed): part of a phone number. Anything else: part of a name.
                var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
                if (digits.Length > 0 && text.All(ch => char.IsAsciiDigit(ch) || ch is ' ' or '+' or '-'))
                {
                    var phone = digits.Length > 10 ? NormalisePhone(digits) ?? digits : digits;
                    query = query.Where(c => c.Phone.Contains(phone));
                }
                else
                {
                    var like = $"%{text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                    query = query.Where(c => EF.Functions.Like(c.Name, like, "\\"));
                }
            }
            var customers = await query.ToListAsync();
            var stats = await StatsAsync(db, customers.Select(c => c.Id).ToList());
            return customers
                .Select(c => ToDto(c, stats))
                .OrderByDescending(c => c.LastVisitAt ?? DateTimeOffset.MinValue).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Clamp(take, 1, MaxResults))
                .ToList();
        });

        g.MapGet("/{id:int}", async (int id, PosDbContext db) =>
        {
            var c = await db.Customers.FindAsync(id);
            return c is null ? ApiErrors.NotFound("Customer") : Results.Ok(ToDto(c, await StatsAsync(db, [id])));
        });

        g.MapPost("", async (CustomerInput input, PosDbContext db) =>
        {
            var c = new Customer();
            if (await ApplyAsync(c, input, db) is { } problem) return problem;
            db.Customers.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/customers/{c.Id}", ToDto(c, []));
        });

        // Change name, phone or standing discount. Bills already made keep the name they had.
        g.MapPut("/{id:int}", async (int id, CustomerInput input, PosDbContext db) =>
        {
            var c = await db.Customers.FindAsync(id);
            if (c is null) return ApiErrors.NotFound("Customer");
            if (await ApplyAsync(c, input, db) is { } problem) return problem;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(c, await StatsAsync(db, [id])));
        });

        return api;
    }

    /// <summary>
    /// The 10-digit mobile number from what staff typed ("98250 12345", "+91 98250-12345", "098250 12345"),
    /// or null if it is not a valid Indian mobile number.
    /// </summary>
    public static string? NormalisePhone(string? input)
    {
        var digits = new string((input ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
        else if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];
        return digits.Length == 10 && digits[0] is >= '6' and <= '9' ? digits : null;
    }

    private static async Task<IResult?> ApplyAsync(Customer c, CustomerInput input, PosDbContext db)
    {
        var name = ApiErrors.Clean(input.Name);
        var phone = NormalisePhone(input.Phone);
        var v = new Validation()
            .Required(name, "name", "Name", MaxName)
            .Check(phone is not null, "phone", "Type a 10-digit mobile number, for example 98250 12345.")
            .Check(input.DiscountBp is >= 0 and <= Money.BasisPoints, "discountBp", "Discount must be between 0% and 100%.");
        if (!v.IsValid) return v.Problem();

        var other = await db.Customers.FirstOrDefaultAsync(x => x.Phone == phone && x.Id != c.Id);
        if (other is not null) return ApiErrors.Conflict($"This phone number is already saved for {other.Name}.");

        c.Name = name!;
        c.Phone = phone!;
        c.DiscountBp = input.DiscountBp;
        return null;
    }

    private record Stats(int Visits, long SpentPaise, DateTimeOffset? LastVisitAt);

    /// <summary>Visits and spending from the customer's bills that were printed or paid and not cancelled.</summary>
    private static async Task<Dictionary<int, Stats>> StatsAsync(PosDbContext db, List<int> ids)
    {
        if (ids.Count == 0) return [];
        var bills = await db.Bills
            .Where(b => b.CustomerId != null && ids.Contains(b.CustomerId.Value) && b.BillNo != null && b.Status != BillStatus.Cancelled)
            .Select(b => new { CustomerId = b.CustomerId!.Value, b.TotalPaise, b.FinalisedAt })
            .ToListAsync();
        return bills.GroupBy(b => b.CustomerId)
            .ToDictionary(x => x.Key, x => new Stats(x.Count(), x.Sum(b => b.TotalPaise), x.Max(b => b.FinalisedAt)));
    }

    private static CustomerDto ToDto(Customer c, Dictionary<int, Stats> stats)
    {
        var s = stats.GetValueOrDefault(c.Id) ?? new Stats(0, 0, null);
        return new CustomerDto(c.Id, c.Name, c.Phone, c.DiscountBp, s.Visits, s.SpentPaise, s.LastVisitAt);
    }
}
