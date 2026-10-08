namespace CipherRAG.Domain.ValueObjects;

/// <summary>
/// Represents a time window for ABAC policy enforcement.
/// HIPAA §164.312(a)(1) — Access control based on work shifts.
/// Supports cross-midnight windows (e.g. 22:00–06:00 night shift).
/// </summary>
public class TimeRange
{
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }

    /// <summary>
    /// Returns true if the request falls within this time window.
    /// Correctly handles overnight shifts where StartTime > EndTime.
    /// </summary>
    public bool IsWithinTimeWindow(DateTimeOffset requestTime)
    {
        var timeOfDay = requestTime.TimeOfDay;

        // Normal window: e.g. 08:00–17:00
        if (StartTime <= EndTime)
            return timeOfDay >= StartTime && timeOfDay <= EndTime;

        // Cross-midnight window: e.g. 22:00–06:00
        return timeOfDay >= StartTime || timeOfDay <= EndTime;
    }
}