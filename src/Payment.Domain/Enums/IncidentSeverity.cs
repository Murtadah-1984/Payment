namespace Payment.Domain.Enums;

/// <summary>
/// Represents the severity level of a payment incident.
/// Used to prioritize incident response and determine notification urgency.
/// </summary>
public enum IncidentSeverity
{
    /// <summary>
    /// Low severity - minor issues with minimal impact.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium severity - moderate impact requiring attention.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High severity - significant impact requiring immediate attention.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical severity - severe impact requiring immediate escalation.
    /// </summary>
    Critical = 3
}

