using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Secrets;

/// <summary>
/// Configuration-based secrets manager for development and fallback scenarios.
/// Reads secrets from IConfiguration (appsettings.json, environment variables, etc.).
/// This is NOT secure for production - use Azure Key Vault, AWS Secrets Manager, or Kubernetes Secrets.
/// </summary>
public class ConfigurationSecretsManager : ISecretsManager
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationSecretsManager> _logger;

    public ConfigurationSecretsManager(
        IConfiguration configuration,
        ILogger<ConfigurationSecretsManager> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key cannot be null or empty", nameof(key));
        }

        var value = _configuration[key];
        
        if (string.IsNullOrEmpty(value))
        {
            _logger.LogWarning("Secret key '{Key}' not found in configuration", key);
            return Task.FromResult<string?>(null);
        }

        _logger.LogDebug("Retrieved secret for key '{Key}' from configuration", key);
        return Task.FromResult<string?>(value);
    }

    public async Task<string> GetSecretRequiredAsync(string key, CancellationToken cancellationToken = default)
    {
        var secret = await GetSecretAsync(key, cancellationToken);
        
        if (string.IsNullOrEmpty(secret))
        {
            throw new KeyNotFoundException($"Required secret '{key}' not found in configuration");
        }

        return secret;
    }

    public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "SetSecretAsync called on ConfigurationSecretsManager for key '{Key}'. " +
            "Configuration-based secrets manager does not support setting secrets. " +
            "Use Azure Key Vault, AWS Secrets Manager, or Kubernetes Secrets for production.",
            key);
        
        throw new NotSupportedException(
            "Configuration-based secrets manager does not support setting secrets. " +
            "Use Azure Key Vault, AWS Secrets Manager, or Kubernetes Secrets for production.");
    }

    public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key cannot be null or empty", nameof(key));
        }

        var value = _configuration[key];
        var exists = !string.IsNullOrEmpty(value);
        
        _logger.LogDebug("Secret key '{Key}' exists: {Exists}", key, exists);
        return Task.FromResult(exists);
    }

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "DeleteSecretAsync called on ConfigurationSecretsManager for key '{Key}'. " +
            "Configuration-based secrets manager does not support deleting secrets. " +
            "Use Azure Key Vault, AWS Secrets Manager, or Kubernetes Secrets for production.",
            key);
        
        throw new NotSupportedException(
            "Configuration-based secrets manager does not support deleting secrets. " +
            "Use Azure Key Vault, AWS Secrets Manager, or Kubernetes Secrets for production.");
    }
}

