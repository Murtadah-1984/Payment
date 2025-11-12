namespace Payment.Application.DTOs;

/// <summary>
/// Request DTO for credential revocation operations.
/// </summary>
public sealed record CredentialRevocationRequest(
    string CredentialId,
    string CredentialType,
    string Reason,
    string? RevokedBy = null)
{
    public static CredentialRevocationRequest Create(
        string credentialId,
        string credentialType,
        string reason,
        string? revokedBy = null)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            throw new ArgumentException("Credential ID cannot be null or empty", nameof(credentialId));
        }

        if (string.IsNullOrWhiteSpace(credentialType))
        {
            throw new ArgumentException("Credential type cannot be null or empty", nameof(credentialType));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be null or empty", nameof(reason));
        }

        return new CredentialRevocationRequest(
            CredentialId: credentialId,
            CredentialType: credentialType,
            Reason: reason,
            RevokedBy: revokedBy);
    }
}

