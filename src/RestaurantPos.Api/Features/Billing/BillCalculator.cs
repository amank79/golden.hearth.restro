using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Features.Billing;

public enum DiscountKind { None, Percent, Amount }

/// <summary>Bill discount: a percent (Value in basis points, 1000 = 10%) or an amount (Value in paise).</summary>
public record Discount(DiscountKind Kind, long Value)
{
    public static readonly Discount None = new(DiscountKind.None, 0);
    public static Discount Percent(int bp) => new(DiscountKind.Percent, bp);
    public static Discount Amount(long paise) => new(DiscountKind.Amount, paise);
}

/// <summary>One bill line for the calculation: price and GST rate as copied onto the bill.</summary>
public record CalcLine(long UnitPricePaise, int Qty, int GstRateBp)
{
    public long AmountPaise => UnitPricePaise * Qty;
}

/// <summary>Tax for all lines with the same GST rate. CGST and SGST are each half of the rate.</summary>
public record TaxGroup(int GstRateBp, long TaxablePaise, long CgstPaise, long SgstPaise)
{
    public int HalfRateBp => GstRateBp / 2;
}

public record BillTotals(
    TaxMode TaxMode,
    long SubtotalPaise,
    long DiscountPaise,
    long TaxablePaise,
    IReadOnlyList<TaxGroup> TaxGroups,
    long CgstPaise,
    long SgstPaise,
    long RoundOffPaise,
    long TotalPaise)
{
    /// <summary>"Tax Invoice" for regular GST, "Bill of Supply" under the composition scheme.</summary>
    public string DocumentTitle => TaxMode == TaxMode.Composition ? "Bill of Supply" : "Tax Invoice";
}

/// <summary>
/// GST bill maths. Everything is whole paise; no floating point.
/// <list type="bullet">
/// <item>Subtotal = Σ price × qty. Discount is a percent of the subtotal (rounded to the paisa) or a fixed amount,
/// never more than the subtotal. Taxable value = subtotal − discount.</item>
/// <item>Regular GST: lines are grouped by GST rate; the discount is shared between the groups in proportion to
/// their amounts (so the shares add up exactly). For each group CGST and SGST are each half the rate on that
/// group's taxable value, each rounded to the nearest paisa.</item>
/// <item>Composition scheme: no tax lines; the document is a "Bill of Supply".</item>
/// <item>Grand total = taxable value + taxes, rounded to the nearest rupee (50 paise goes up); the difference is the
/// round-off line.</item>
/// </list>
/// </summary>
public static class BillCalculator
{
    public static BillTotals Calculate(IEnumerable<CalcLine> lines, Discount discount, TaxMode mode)
    {
        var list = lines.ToList();
        if (list.Any(l => l.Qty <= 0 || l.UnitPricePaise < 0 || l.GstRateBp < 0 || l.GstRateBp % 2 != 0))
        {
            throw new ArgumentException("Lines need a positive quantity, a price of at least zero and an even GST rate.", nameof(lines));
        }

        var subtotal = list.Sum(l => l.AmountPaise);
        var discountPaise = DiscountAmount(subtotal, discount);
        var taxable = subtotal - discountPaise;

        var groups = new List<TaxGroup>();
        if (mode == TaxMode.Regular && subtotal > 0)
        {
            var byRate = list.GroupBy(l => l.GstRateBp).OrderBy(g => g.Key)
                .Select(g => (Rate: g.Key, Amount: g.Sum(l => l.AmountPaise))).ToList();
            var shares = Share(discountPaise, byRate.Select(g => g.Amount).ToList());
            for (var i = 0; i < byRate.Count; i++)
            {
                var groupTaxable = byRate[i].Amount - shares[i];
                var half = Money.ApplyRate(groupTaxable, byRate[i].Rate / 2);
                groups.Add(new TaxGroup(byRate[i].Rate, groupTaxable, half, half));
            }
        }

        var cgst = groups.Sum(g => g.CgstPaise);
        var sgst = groups.Sum(g => g.SgstPaise);
        var raw = taxable + cgst + sgst;
        var total = Money.RoundToRupee(raw);
        return new BillTotals(mode, subtotal, discountPaise, taxable, groups, cgst, sgst, total - raw, total);
    }

    /// <summary>The discount in paise, never negative and never more than the subtotal.</summary>
    public static long DiscountAmount(long subtotal, Discount discount)
    {
        var amount = discount.Kind switch
        {
            DiscountKind.Percent => Money.ApplyRate(subtotal, (int)Math.Clamp(discount.Value, 0, Money.BasisPoints)),
            DiscountKind.Amount => discount.Value,
            _ => 0,
        };
        return Math.Clamp(amount, 0, subtotal);
    }

    /// <summary>
    /// Splits <paramref name="total"/> across parts in proportion to <paramref name="weights"/> so the shares add up
    /// exactly (largest remainder method; ties go to the earlier part).
    /// </summary>
    public static long[] Share(long total, IReadOnlyList<long> weights)
    {
        var sum = weights.Sum();
        var shares = new long[weights.Count];
        if (sum == 0 || total == 0) return shares;

        var remainders = new (long Remainder, int Index)[weights.Count];
        long given = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            var product = (Int128)total * weights[i]; // no overflow even for huge bills
            shares[i] = (long)(product / sum);
            remainders[i] = ((long)(product % sum), i);
            given += shares[i];
        }
        foreach (var (_, index) in remainders.OrderByDescending(x => x.Remainder).ThenBy(x => x.Index).Take((int)(total - given)))
        {
            shares[index]++;
        }
        return shares;
    }
}
