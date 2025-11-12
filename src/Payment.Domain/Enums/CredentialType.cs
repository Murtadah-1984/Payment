namespace Payment.Domain.Enums;

/// <summary>
/// Represents the type of credential that can be revoked.
/// Used for categorizing and managing different credential types.
/// </summary>
public enum CredentialType
{
    /// <summary>
    /// API key credential.
    /// </summary>
    ApiKey = 0,

    /// <summary>
    /// JWT token credential.
    /// </summary>
    JwtToken = 1,

    /// <summary>
    /// OAuth2 access token.
    /// </summary>
    OAuth2Token = 2,

    /// <summary>
    /// Webhook secret.
    /// </summary>
    WebhookSecret = 3,

    /// <summary>
    /// Database connection string.
    /// </summary>
    DatabaseConnection = 4,

    /// <summary>
    /// Payment provider API key.
    /// </summary>
    PaymentProviderKey = 5,

    /// <summary>
    /// JWT signing key.
    /// </summary>
    JwtSigningKey = 6,

    /// <summary>
    /// Other credential type.
    /// </summary>
    Other = 99
}

