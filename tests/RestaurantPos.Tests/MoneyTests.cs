using RestaurantPos.Api.Features.Billing;

namespace RestaurantPos.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(10, 4, 3)]   // 2.5 -> 3 (half goes up)
    [InlineData(9, 4, 2)]    // 2.25 -> 2
    [InlineData(11, 4, 3)]   // 2.75 -> 3
    [InlineData(-10, 4, -3)] // -2.5 -> -3 (away from zero)
    [InlineData(-9, 4, -2)]
    [InlineData(0, 7, 0)]
    public void DivRound_rounds_half_away_from_zero(long n, long d, long expected) =>
        Assert.Equal(expected, Money.DivRound(n, d));

    [Theory]
    [InlineData(10550, 250, 264)] // 263.75 -> 264
    [InlineData(20, 250, 1)]      // 0.5 -> 1
    [InlineData(10, 250, 0)]      // 0.25 -> 0
    [InlineData(104400, 250, 2610)]
    public void ApplyRate_rounds_to_nearest_paisa(long paise, int bp, long expected) =>
        Assert.Equal(expected, Money.ApplyRate(paise, bp));

    [Theory]
    [InlineData(12350, 12400)] // exactly 50 paise goes up
    [InlineData(12349, 12300)]
    [InlineData(12351, 12400)]
    [InlineData(12300, 12300)]
    [InlineData(0, 0)]
    [InlineData(49, 0)]
    public void RoundToRupee(long paise, long expected) => Assert.Equal(expected, Money.RoundToRupee(paise));

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(5, "0.05")]
    [InlineData(123450, "1,234.50")]
    [InlineData(10000000, "1,00,000.00")]
    [InlineData(123456789, "12,34,567.89")]
    [InlineData(99999, "999.99")]
    [InlineData(-2000, "-20.00")]
    [InlineData(-123456, "-1,234.56")]
    public void Format_uses_indian_grouping(long paise, string expected) => Assert.Equal(expected, Money.Format(paise));

    [Theory]
    [InlineData(500, "5")]
    [InlineData(250, "2.5")]
    [InlineData(1250, "12.5")]
    [InlineData(1800, "18")]
    [InlineData(0, "0")]
    public void Percent(int bp, string expected) => Assert.Equal(expected, Money.Percent(bp));
}
