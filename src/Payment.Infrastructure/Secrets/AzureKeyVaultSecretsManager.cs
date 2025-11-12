using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Secrets;

/// <summary>
/// Azure Key Vault secrets manager for production environments.
/// Uses DefaultAzureCredential for authentication (supports Managed Identity, Azure CLI, etc.).
/// </summary>
public class AzureKeyVaultSecretsManager : ISecretsManager
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<AzureKeyVaultSecretsManager> _logger;
    private readonly string _keyPrefix;

    public AzureKeyVaultSecretsManager(
        IConfiguration configuration,
        ILogger<AzureKeyVaultSecretsManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var keyVaultUri = configuration["KeyVault:Uri"] 
            ?? throw new InvalidOperationException("KeyVault:Uri configuration is required for Azure Key Vault secrets manager");

        _keyPrefix = configuration["KeyVault:KeyPrefix"] ?? string.Empty;

        // Use DefaultAzureCredential which supports:
        // - Managed Identity (for Azure services)
        // - Azure CLI (for local development)
        // - Visual Studio credentials
        // - Environment variables
        TokenCredential credential = new DefaultAzureCredential();

        _secretClient = new SecretClient(new Uri(keyVaultUri), credential);
        
        _logger.LogInformation("Azure Key Vault secrets manager initialized with URI: {KeyVaultUri}", keyVaultUri);
    }

    private string GetKeyVaultKey(string key)
    {
        // Convert configuration key format to Key Vault secret name
        // e.g., "JwtSettings:SecretKey" -> "JwtSettings-SecretKey" or "Payment-JwtSettings-SecretKey"
        var normalizedKey = key.Replace(":", "-");
        
        if (!string.IsNullOrEmpty(_keyPrefix))
        {
            return $"{_keyPrefix}-{normalizedKey}";
        }
        
        return normalizedKey;
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key cannot be null or empty", nameof(key));
        }

        try
        {
            var keyVaultKey = GetKeyVaultKey(key);
            var secret = await _secretClient.GetSecretAsync(keyVaultKey, cancellationToken: cancellationToken);
            
            _logger.LogDebug("Retrieved secret for key '{Key}' from Azure Key Vault", key);
            return secret.Value.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret key '{Key}' not found in Azure Key Vault", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret '{Key}' from Azure Key Vault", key);
            throw;
        }
    }

    public async Task<string> GetSecretRequiredAsync(string key, CancellationToken cancellationToken = default)
    {
        var secret = await GetSecretAsync(key, cancellationToken);
        
        if (string.IsNullOrEmpty(secret))
        {
            throw new KeyNotFoundException($"Required secret '{key}' not found in Azure Key Vault");
        }

        return secret;
    }

    public async Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key cannot be null or empty", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Secret value cannot be null or empty", nameof(value));
        }

        try
        {
            var keyVaultKey = GetKeyVaultKey(key);
            await _secretClient.SetSecretAsync(keyVaultKey, value, cancellationToken);
            
            _logger.LogInformation("Secret '{Key}' set in Azure Key Vault", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting secret '{Key}' in Azure Key Vault", key);
            throw;
        }
    }

    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key cannot be null or empty", nameof(key));
        }

        try
        {
            var keyVaultKey = GetKeyVaultKey(key);
            var secret = await _secretClient.GetSecretAsync(keyVaultKey, cancellationToken: cancellationToken);
            
            _logger.LogDebug("Secret key '{Key}' exists in Azure Key Vault", key);
            return secret != null;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Secret key '{Key}' does not exist in Azure Key Vault", key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if secret '{Key}' exists in Azure Key Vault", key);
            throw;
        }
    }

    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key cannot be null or empty", nameof(key));
        }

        try
        {
            var keyVaultKey = GetKeyVaultKey(key);
            await _secretClient.StartDeleteSecretAsync(keyVaultKey, cancellationToken);
            
            _logger.LogInformation("Secret '{Key}' deleted from Azure Key Vault", key);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret key '{Key}' not found in Azure Key Vault for deletion", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting secret '{Key}' from Azure Key Vault", key);
            throw;
        }
    }
}

