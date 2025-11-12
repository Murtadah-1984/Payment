namespace Payment.Domain.ValueObjects;

/// <summary>
/// Value object representing the result of a 3D Secure authentication.
/// Immutable and follows Value Object pattern.
/// </summary>
public sealed record ThreeDSecureResult
{
    /// <summary>
    /// Initializes a new instance of ThreeDSecureResult.
    /// </summary>
    /// <param name="authenticated">Whether authentication was successful</param>
    /// <param name="cavv">The Cardholder Authentication Verification Value</param>
    /// <param name="eci">The Electronic Commerce Indicator</param>
    /// <param name="xid">The transaction identifier</param>
    /// <param name="version">The 3DS protocol version used</param>
    /// <param name="ares">The Authentication Response (ARes) from the ACS</param>
    /// <param name="failureReason">Reason for failure if authentication failed</param>
    public ThreeDSecureResult(
        bool authenticated,
        string? cavv = null,
        string? eci = null,
        string? xid = null,
        string? version = null,
        string? ares = null,
        string? failureReason = null)
    {
        Authenticated = authenticated;
        Cavv = cavv;
        Eci = eci;
        Xid = xid;
        Version = version;
        Ares = ares;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Whether the 3DS authentication was successful
    /// </summary>
    public bool Authenticated { get; init; }

    /// <summary>
    /// Cardholder Authentication Verification Value (CAVV)
    /// </summary>
    public string? Cavv { get; init; }

    /// <summary>
    /// Electronic Commerce Indicator (ECI)
    /// </summary>
    public string? Eci { get; init; }

    /// <summary>
    /// Transaction identifier (XID)
    /// </summary>
    public string? Xid { get; init; }

    /// <summary>
    /// The 3DS protocol version used
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Authentication Response (ARes) from the ACS
    /// </summary>
    public string? Ares { get; init; }

    /// <summary>
    /// Reason for failure if authentication failed
    /// </summary>
    public string? FailureReason { get; init; }
}

