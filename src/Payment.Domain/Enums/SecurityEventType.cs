namespace Payment.Domain.Enums;

/// <summary>
/// Represents the type of security event that occurred.
/// Used for categorizing and analyzing security incidents.
/// </summary>
public enum SecurityEventType
{
    /// <summary>
    /// Failed authentication attempt.
    /// </summary>
    AuthenticationFailure = 0,

    /// <summary>
    /// Successful authentication from suspicious location.
    /// </summary>
    SuspiciousAuthentication = 1,

    /// <summary>
    /// Unauthorized access attempt.
    /// </summary>
    UnauthorizedAccess = 2,

    /// <summary>
    /// Rate limit exceeded.
    /// </summary>
    RateLimitExceeded = 3,

    /// <summary>
    /// Suspicious payment pattern detected.
    /// </summary>
    SuspiciousPaymentPattern = 4,

    /// <summary>
    /// API key compromised or revoked.
    /// </summary>
    CredentialCompromise = 5,

    /// <summary>
    /// Data breach or unauthorized data access.
    /// </summary>
    DataBreach = 6,

    /// <summary>
    /// Malicious payload detected in request.
    /// </summary>
    MaliciousPayload = 7,

    /// <summary>
    /// Distributed denial of service attack.
    /// </summary>
    DDoS = 8,

    /// <summary>
    /// Other security-related event.
    /// </summary>
    Other = 99
}

