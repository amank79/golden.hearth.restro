namespace RestaurantPos.Api.Features.Billing;

/// <summary>
/// Dates in India time (Asia/Kolkata, UTC+05:30, no daylight saving). Used for the financial year, bill dates and
/// "today", so they are right whatever time zone the laptop is set to.
/// </summary>
public static class IndiaTime
{
    public static readonly TimeZoneInfo Zone = FindZone();

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) return zone;
        }
        return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "IST");
    }

    public static DateTimeOffset ToIndia(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Zone);

    public static DateOnly DateOf(DateTimeOffset time) => DateOnly.FromDateTime(ToIndia(time).DateTime);

    public static DateOnly Today(TimeProvider clock) => DateOf(clock.GetUtcNow());

    /// <summary>The instant an India calendar day starts (inclusive) and the next one starts (exclusive).</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) DayRange(DateOnly day)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var next = start.AddDays(1);
        return (new DateTimeOffset(start, Zone.GetUtcOffset(start)), new DateTimeOffset(next, Zone.GetUtcOffset(next)));
    }
}
