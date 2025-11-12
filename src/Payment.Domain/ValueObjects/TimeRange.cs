namespace Payment.Domain.ValueObjects;

/// <summary>
/// Represents a time range for querying incident metrics.
/// Immutable value object following DDD principles.
/// </summary>
public sealed record TimeRange(DateTime Start, DateTime End)
{
    public TimeRange(DateTime start, DateTime end) : this()
    {
        if (start >= end)
        {
            throw new ArgumentException("Start time must be before end time", nameof(start));
        }

        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the duration of the time range.
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Checks if a date time falls within this time range.
    /// </summary>
    public bool Contains(DateTime dateTime)
    {
        return dateTime >= Start && dateTime <= End;
    }

    /// <summary>
    /// Creates a time range for the last N hours from now.
    /// </summary>
    public static TimeRange LastHours(int hours)
    {
        var end = DateTime.UtcNow;
        var start = end.AddHours(-hours);
        return new TimeRange(start, end);
    }

    /// <summary>
    /// Creates a time range for the last N days from now.
    /// </summary>
    public static TimeRange LastDays(int days)
    {
        var end = DateTime.UtcNow;
        var start = end.AddDays(-days);
        return new TimeRange(start, end);
    }
}

