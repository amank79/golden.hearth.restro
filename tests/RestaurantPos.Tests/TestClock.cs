namespace RestaurantPos.Tests;

/// <summary>A clock the tests can set and move forward.</summary>
public class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public void Advance(TimeSpan by) => Now += by;

    /// <summary>A time given as India local time (UTC+05:30).</summary>
    public static DateTimeOffset India(int year, int month, int day, int hour = 12, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, TimeSpan.FromHours(5.5));
}
