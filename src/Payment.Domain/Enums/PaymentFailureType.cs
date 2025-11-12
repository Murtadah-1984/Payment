namespace Payment.Domain.Enums;

/// <summary>
/// Represents the type of payment failure for incident response.
/// Used to categorize and assess payment failures for appropriate response.
/// </summary>
public enum PaymentFailureType
{
    /// <summary>
    /// Provider is unavailable or circuit breaker is open.
    /// </summary>
    ProviderUnavailable = 0,

    /// <summary>
    /// Provider returned an error response.
    /// </summary>
    ProviderError = 1,

    /// <summary>
    /// Payment processing timed out.
    /// </summary>
    Timeout = 2,

    /// <summary>
    /// Payment was declined by the provider.
    /// </summary>
    Declined = 3,

    /// <summary>
    /// Network connectivity issue.
    /// </summary>
    NetworkError = 4,

    /// <summary>
    /// Authentication or authorization failure with provider.
    /// </summary>
    AuthenticationError = 5,

    /// <summary>
    /// Invalid payment data or configuration.
    /// </summary>
    ValidationError = 6,

    /// <summary>
    /// Unknown or unclassified failure.
    /// </summary>
    Unknown = 7
}

