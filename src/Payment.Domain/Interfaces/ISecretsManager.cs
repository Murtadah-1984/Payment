namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for secure secrets management.
/// Follows Dependency Inversion Principle - Domain layer defines the contract.
/// Supports multiple implementations: Azure Key Vault, AWS Secrets Manager, Kubernetes Secrets, Configuration (fallback).
/// </summary>
public interface ISecretsManager
{
    /// <summary>
    /// Gets a secret value by key.
    /// </summary>
    /// <param name="key">The secret key (e.g., "JwtSettings:SecretKey", "PaymentProviders:ZainCash:MerchantSecret")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The secret value, or null if not found</returns>
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a secret value by key, throwing an exception if not found.
    /// </summary>
    /// <param name="key">The secret key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The secret value</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the secret is not found</exception>
    Task<string> GetSecretRequiredAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or updates a secret value.
    /// </summary>
    /// <param name="key">The secret key</param>
    /// <param name="value">The secret value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a secret exists.
    /// </summary>
    /// <param name="key">The secret key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the secret exists, false otherwise</returns>
    Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret.
    /// </summary>
    /// <param name="key">The secret key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);
}

