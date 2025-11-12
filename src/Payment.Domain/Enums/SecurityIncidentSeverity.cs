namespace Payment.Domain.Enums;

/// <summary>
/// Represents the severity level of a security incident.
/// Used to prioritize security incident response and determine containment strategies.
/// </summary>
public enum SecurityIncidentSeverity
{
    /// <summary>
    /// Low severity - minor security events with minimal impact.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium severity - moderate security impact requiring attention.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High severity - significant security impact requiring immediate attention.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical severity - severe security impact requiring immediate escalation and containment.
    /// </summary>
    Critical = 3
}

