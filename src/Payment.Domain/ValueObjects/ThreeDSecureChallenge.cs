namespace Payment.Domain.ValueObjects;

/// <summary>
/// Value object representing a 3D Secure authentication challenge.
/// Immutable and follows Value Object pattern.
/// </summary>
public sealed record ThreeDSecureChallenge
{
    /// <summary>
    /// Initializes a new instance of ThreeDSecureChallenge.
    /// </summary>
    /// <param name="acsUrl">The Access Control Server (ACS) URL for the challenge</param>
    /// <param name="pareq">The Payment Authentication Request (PAReq) data</param>
    /// <param name="md">The merchant data to be returned after authentication</param>
    /// <param name="termUrl">The return URL after authentication</param>
    /// <param name="version">The 3DS protocol version (e.g., "2.1.0", "2.2.0")</param>
    public ThreeDSecureChallenge(
        string acsUrl,
        string pareq,
        string md,
        string termUrl,
        string version = "2.2.0")
    {
        if (string.IsNullOrWhiteSpace(acsUrl))
            throw new ArgumentException("ACS URL cannot be null or empty", nameof(acsUrl));
        
        if (string.IsNullOrWhiteSpace(pareq))
            throw new ArgumentException("PAReq cannot be null or empty", nameof(pareq));
        
        if (string.IsNullOrWhiteSpace(md))
            throw new ArgumentException("Merchant data cannot be null or empty", nameof(md));
        
        if (string.IsNullOrWhiteSpace(termUrl))
            throw new ArgumentException("Term URL cannot be null or empty", nameof(termUrl));

        AcsUrl = acsUrl;
        Pareq = pareq;
        Md = md;
        TermUrl = termUrl;
        Version = version;
    }

    /// <summary>
    /// The Access Control Server (ACS) URL where the user will be redirected for authentication
    /// </summary>
    public string AcsUrl { get; init; }

    /// <summary>
    /// The Payment Authentication Request (PAReq) data to be sent to the ACS
    /// </summary>
    public string Pareq { get; init; }

    /// <summary>
    /// The merchant data that will be returned after authentication
    /// </summary>
    public string Md { get; init; }

    /// <summary>
    /// The return URL after authentication is complete
    /// </summary>
    public string TermUrl { get; init; }

    /// <summary>
    /// The 3DS protocol version
    /// </summary>
    public string Version { get; init; }
}

