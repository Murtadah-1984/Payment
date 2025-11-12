using Payment.Domain.Entities;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Service interface for revoking compromised credentials.
/// Follows Interface Segregation Principle - focused on credential revocation only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface ICredentialRevocationService
{
    /// <summary>
    /// Revokes an API key.
    /// </summary>
    /// <param name="apiKeyId">The API key ID to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeApiKeyAsync(
        string apiKeyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a JWT token.
    /// </summary>
    /// <param name="tokenId">The token ID to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeJwtTokenAsync(
        string tokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates secrets (database connection strings, payment provider keys, etc.).
    /// </summary>
    /// <param name="secretName">The name of the secret to rotate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RotateSecretsAsync(
        string secretName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a credential is revoked.
    /// </summary>
    /// <param name="credentialId">The credential ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the credential is revoked, false otherwise.</returns>
    Task<bool> IsRevokedAsync(
        string credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all revoked credentials, optionally filtered by date.
    /// </summary>
    /// <param name="since">Optional date filter - only return credentials revoked after this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of revoked credentials.</returns>
    Task<IEnumerable<RevokedCredential>> GetRevokedCredentialsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default);
}

