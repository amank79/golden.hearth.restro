using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;

namespace RestaurantPos.Tests;

public class BillCalculatorTests
{
    private static CalcLine L(long rupees, int qty, int rateBp = 500) => new(rupees * 100, qty, rateBp);

    [Fact]
    public void Regular_gst_on_a_simple_bill()
    {
        // Veg Biryani 2 × 240, Masala Chaas 2 × 60, Gulab Jamun 2 × 80 (the demo's bill preview).
        var t = BillCalculator.Calculate([L(240, 2), L(60, 2), L(80, 2)], Discount.None, TaxMode.Regular);

        Assert.Equal(76000, t.SubtotalPaise);
        Assert.Equal(0, t.DiscountPaise);
        Assert.Equal(76000, t.TaxablePaise);
        Assert.Equal(1900, t.CgstPaise);
        Assert.Equal(1900, t.SgstPaise);
        Assert.Equal(0, t.RoundOffPaise);
        Assert.Equal(79800, t.TotalPaise);
        Assert.Equal("Tax Invoice", t.DocumentTitle);
        var g = Assert.Single(t.TaxGroups);
        Assert.Equal(500, g.GstRateBp);
        Assert.Equal(250, g.HalfRateBp);
    }

    [Fact]
    public void Each_tax_is_rounded_to_the_paisa_and_the_total_to_the_rupee()
    {
        // 105.50 taxable: 2.5% = 2.6375 -> 2.64 each; 105.50 + 5.28 = 110.78 -> 111.00, round off +0.22.
        var t = BillCalculator.Calculate([new CalcLine(10550, 1, 500)], Discount.None, TaxMode.Regular);
        Assert.Equal(264, t.CgstPaise);
        Assert.Equal(264, t.SgstPaise);
        Assert.Equal(22, t.RoundOffPaise);
        Assert.Equal(11100, t.TotalPaise);
    }

    [Fact]
    public void Round_off_can_be_negative()
    {
        // 1160 - 10% = 1044; 2.5% = 26.10 each; 1096.20 -> 1096, round off -0.20.
        var t = BillCalculator.Calculate([L(320, 1), L(360, 1), L(80, 3), L(160, 1), L(40, 2)], Discount.Percent(1000), TaxMode.Regular);
        Assert.Equal(116000, t.SubtotalPaise);
        Assert.Equal(11600, t.DiscountPaise);
        Assert.Equal(104400, t.TaxablePaise);
        Assert.Equal(2610, t.CgstPaise);
        Assert.Equal(2610, t.SgstPaise);
        Assert.Equal(-20, t.RoundOffPaise);
        Assert.Equal(109600, t.TotalPaise);
    }

    [Fact]
    public void Exactly_half_a_rupee_rounds_up()
    {
        // 100.00 at 0.5% (0.25% each) = 0.25 each -> total 100.50 -> 101, round off +0.50.
        var t = BillCalculator.Calculate([L(100, 1, 50)], Discount.None, TaxMode.Regular);
        Assert.Equal(25, t.CgstPaise);
        Assert.Equal(50, t.RoundOffPaise);
        Assert.Equal(10100, t.TotalPaise);
    }

    [Fact]
    public void Percent_discount_is_rounded_to_the_paisa()
    {
        // 12.5% of 99.99 = 12.49875 -> 12.50
        var t = BillCalculator.Calculate([new CalcLine(9999, 1, 500)], Discount.Percent(1250), TaxMode.Regular);
        Assert.Equal(1250, t.DiscountPaise);
        Assert.Equal(8749, t.TaxablePaise);
    }

    [Fact]
    public void Amount_discount()
    {
        var t = BillCalculator.Calculate([L(500, 1)], Discount.Amount(5000), TaxMode.Regular);
        Assert.Equal(5000, t.DiscountPaise);
        Assert.Equal(45000, t.TaxablePaise);
        Assert.Equal(1125, t.CgstPaise);
        Assert.Equal(47250, t.TaxablePaise + t.CgstPaise + t.SgstPaise);
        Assert.Equal(47300, t.TotalPaise);
        Assert.Equal(50, t.RoundOffPaise);
    }

    [Fact]
    public void Discount_is_never_more_than_the_subtotal()
    {
        var t = BillCalculator.Calculate([L(100, 1)], Discount.Amount(50000), TaxMode.Regular);
        Assert.Equal(10000, t.DiscountPaise);
        Assert.Equal(0, t.TaxablePaise);
        Assert.Equal(0, t.TotalPaise);

        var p = BillCalculator.Calculate([L(100, 1)], Discount.Percent(15000), TaxMode.Regular);
        Assert.Equal(10000, p.DiscountPaise);
    }

    [Fact]
    public void Full_discount_gives_a_zero_bill()
    {
        var t = BillCalculator.Calculate([L(100, 2)], Discount.Percent(10000), TaxMode.Regular);
        Assert.Equal(0, t.TotalPaise);
        Assert.Equal(0, t.CgstPaise);
    }

    [Fact]
    public void Composition_mode_has_no_tax_and_is_a_bill_of_supply()
    {
        var t = BillCalculator.Calculate([new CalcLine(10550, 1, 500)], Discount.None, TaxMode.Composition);
        Assert.Empty(t.TaxGroups);
        Assert.Equal(0, t.CgstPaise);
        Assert.Equal(0, t.SgstPaise);
        Assert.Equal(10550, t.TaxablePaise);
        Assert.Equal(50, t.RoundOffPaise); // 105.50 -> 106.00: 50 paise goes up
        Assert.Equal(10600, t.TotalPaise);
        Assert.Equal("Bill of Supply", t.DocumentTitle);
    }

    [Fact]
    public void Composition_mode_rounds_the_total()
    {
        var t = BillCalculator.Calculate([new CalcLine(10549, 1, 500)], Discount.Amount(100), TaxMode.Composition);
        Assert.Equal(10449, t.TaxablePaise);
        Assert.Equal(10400, t.TotalPaise);
        Assert.Equal(-49, t.RoundOffPaise);
    }

    [Fact]
    public void Empty_bill_is_all_zero()
    {
        var t = BillCalculator.Calculate([], Discount.Percent(1000), TaxMode.Regular);
        Assert.Equal(0, t.SubtotalPaise);
        Assert.Equal(0, t.DiscountPaise);
        Assert.Equal(0, t.TotalPaise);
        Assert.Empty(t.TaxGroups);
    }

    [Fact]
    public void Different_rates_are_taxed_separately_and_the_discount_is_shared()
    {
        // Food 900 at 5%, packaged item 100 at 18%; 10% discount = 100, shared 90 / 10.
        var t = BillCalculator.Calculate([L(900, 1, 500), L(100, 1, 1800)], Discount.Percent(1000), TaxMode.Regular);

        Assert.Equal(2, t.TaxGroups.Count);
        var five = t.TaxGroups[0];
        var eighteen = t.TaxGroups[1];
        Assert.Equal((500, 81000L, 2025L, 2025L), (five.GstRateBp, five.TaxablePaise, five.CgstPaise, five.SgstPaise));
        Assert.Equal((1800, 9000L, 810L, 810L), (eighteen.GstRateBp, eighteen.TaxablePaise, eighteen.CgstPaise, eighteen.SgstPaise));
        Assert.Equal(90000, t.TaxablePaise);
        Assert.Equal(2835, t.CgstPaise);
        Assert.Equal(95670, t.TaxablePaise + t.CgstPaise + t.SgstPaise);
        Assert.Equal(95700, t.TotalPaise);
        Assert.Equal(30, t.RoundOffPaise);
    }

    [Fact]
    public void Zero_rate_lines_have_no_tax()
    {
        var t = BillCalculator.Calculate([L(100, 1, 0)], Discount.None, TaxMode.Regular);
        Assert.Equal(0, Assert.Single(t.TaxGroups).CgstPaise);
        Assert.Equal(10000, t.TotalPaise);
    }

    [Theory]
    [InlineData(100, 0, 500)]
    [InlineData(100, -1, 500)]
    [InlineData(100, 1, 501)]
    [InlineData(-100, 1, 500)]
    public void Invalid_lines_are_rejected(long price, int qty, int rate) =>
        Assert.Throws<ArgumentException>(() => BillCalculator.Calculate([new CalcLine(price, qty, rate)], Discount.None, TaxMode.Regular));

    [Fact]
    public void Share_adds_up_exactly()
    {
        Assert.Equal([34, 33, 33], BillCalculator.Share(100, [1, 1, 1]));
        Assert.Equal([0, 0], BillCalculator.Share(0, [5, 5]));
        Assert.Equal([0, 0], BillCalculator.Share(10, [0, 0]));
        Assert.Equal([7, 3], BillCalculator.Share(10, [70, 30]));
        Assert.Equal([1, 0, 0], BillCalculator.Share(1, [1, 1, 1]));
    }

    [Fact]
    public void Random_bills_keep_every_rule()
    {
        var random = new Random(20261011);
        int[] rates = [0, 500, 1200, 1800];
        for (var n = 0; n < 2000; n++)
        {
            var lines = Enumerable.Range(0, random.Next(1, 12))
                .Select(_ => new CalcLine(random.Next(0, 100_000), random.Next(1, 20), rates[random.Next(rates.Length)]))
                .ToList();
            var discount = random.Next(3) switch
            {
                0 => Discount.None,
                1 => Discount.Percent(random.Next(0, 10_001)),
                _ => Discount.Amount(random.Next(0, 500_000)),
            };
            var mode = random.Next(4) == 0 ? TaxMode.Composition : TaxMode.Regular;
            var t = BillCalculator.Calculate(lines, discount, mode);

            Assert.Equal(lines.Sum(l => l.UnitPricePaise * l.Qty), t.SubtotalPaise);
            Assert.InRange(t.DiscountPaise, 0, t.SubtotalPaise);
            Assert.Equal(t.SubtotalPaise - t.DiscountPaise, t.TaxablePaise);
            Assert.Equal(t.CgstPaise, t.SgstPaise);
            Assert.Equal(0, t.TotalPaise % 100);
            Assert.InRange(t.RoundOffPaise, -49, 50);
            Assert.Equal(t.TaxablePaise + t.CgstPaise + t.SgstPaise + t.RoundOffPaise, t.TotalPaise);
            if (mode == TaxMode.Composition)
            {
                Assert.Empty(t.TaxGroups);
            }
            else if (t.SubtotalPaise > 0)
            {
                Assert.Equal(t.TaxablePaise, t.TaxGroups.Sum(g => g.TaxablePaise));
                Assert.All(t.TaxGroups, g => Assert.Equal(Money.ApplyRate(g.TaxablePaise, g.GstRateBp / 2), g.CgstPaise));
            }
        }
    }
}
