namespace Payment.Domain.Enums;

/// <summary>
/// Represents the type of security threat identified in an incident.
/// Used to categorize threats and determine appropriate response strategies.
/// </summary>
public enum SecurityThreatType
{
    /// <summary>
    /// Credential-based attack (brute force, credential stuffing).
    /// </summary>
    CredentialAttack = 0,

    /// <summary>
    /// Unauthorized access attempt.
    /// </summary>
    UnauthorizedAccess = 1,

    /// <summary>
    /// Data exfiltration or breach.
    /// </summary>
    DataExfiltration = 2,

    /// <summary>
    /// Denial of service attack.
    /// </summary>
    DenialOfService = 3,

    /// <summary>
    /// Malware or malicious code execution.
    /// </summary>
    Malware = 4,

    /// <summary>
    /// Payment fraud or suspicious transaction pattern.
    /// </summary>
    PaymentFraud = 5,

    /// <summary>
    /// Insider threat or unauthorized internal access.
    /// </summary>
    InsiderThreat = 6,

    /// <summary>
    /// Unknown or unclassified threat.
    /// </summary>
    Unknown = 99
}

