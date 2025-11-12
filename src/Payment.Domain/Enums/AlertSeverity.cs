namespace Payment.Domain.Enums;

/// <summary>
/// Represents the severity level of an alert.
/// Used to prioritize alerts and determine notification channels.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Low severity - informational alerts with minimal impact.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium severity - warnings requiring attention.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High severity - significant issues requiring immediate attention.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical severity - severe issues requiring immediate escalation and response.
    /// </summary>
    Critical = 3
}

