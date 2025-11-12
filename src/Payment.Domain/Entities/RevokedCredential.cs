using Payment.Domain.Enums;

namespace Payment.Domain.Entities;

/// <summary>
/// Entity representing a revoked credential.
/// Used for audit trail and tracking credential revocations.
/// Follows Domain-Driven Design entity pattern.
/// </summary>
public class RevokedCredential
{
    /// <summary>
    /// Gets or sets the unique identifier of the revoked credential.
    /// </summary>
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of credential.
    /// </summary>
    public CredentialType Type { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the credential was revoked.
    /// </summary>
    public DateTime RevokedAt { get; set; }

    /// <summary>
    /// Gets or sets the reason for revocation.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user or system that revoked the credential.
    /// </summary>
    public string? RevokedBy { get; set; }

    /// <summary>
    /// Gets or sets the expiration date of the credential (if applicable).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

