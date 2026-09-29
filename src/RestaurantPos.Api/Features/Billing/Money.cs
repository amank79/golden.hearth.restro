using System.Globalization;

namespace RestaurantPos.Api.Features.Billing;

/// <summary>Integer money helpers. Amounts are whole paise (long); rates are basis points (500 = 5%).</summary>
public static class Money
{
    public const int BasisPoints = 10_000; // 100%

    /// <summary>numerator / denominator rounded to the nearest whole number; exact halves go away from zero.</summary>
    public static long DivRound(long numerator, long denominator)
    {
        if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
        var q = Math.DivRem(Math.Abs(numerator), denominator, out var r);
        if (r * 2 >= denominator) q++;
        return numerator < 0 ? -q : q;
    }

    /// <summary>amount × rate, rounded to the nearest paisa.</summary>
    public static long ApplyRate(long paise, int rateBp) => DivRound(paise * rateBp, BasisPoints);

    /// <summary>Rounds to the nearest whole rupee (50 paise and more go up).</summary>
    public static long RoundToRupee(long paise) => DivRound(paise, 100) * 100;

    /// <summary>Rupees with Indian digit grouping and 2 decimals, without the ₹ sign: 123456789 -> "12,34,567.89".</summary>
    public static string Format(long paise)
    {
        var sign = paise < 0 ? "-" : "";
        var abs = Math.Abs(paise);
        var rupees = (abs / 100).ToString(CultureInfo.InvariantCulture);
        var decimals = (abs % 100).ToString("00", CultureInfo.InvariantCulture);
        return $"{sign}{GroupIndian(rupees)}.{decimals}";
    }

    /// <summary>"1234567" -> "12,34,567": last three digits, then groups of two.</summary>
    private static string GroupIndian(string digits)
    {
        if (digits.Length <= 3) return digits;
        var head = digits[..^3];
        var tail = digits[^3..];
        var groups = new List<string>();
        for (var i = head.Length; i > 0; i -= 2) groups.Insert(0, head[Math.Max(0, i - 2)..i]);
        return string.Join(",", groups) + "," + tail;
    }

    /// <summary>A rate in basis points as a percent: 250 -> "2.5", 500 -> "5", 1250 -> "12.5".</summary>
    public static string Percent(int bp) =>
        (bp / 100m).ToString("0.##", CultureInfo.InvariantCulture);
}
