using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Secrets;

/// <summary>
/// AWS Secrets Manager implementation for production environments.
/// Uses AWS SDK for .NET with default credential chain (IAM roles, environment variables, etc.).
/// </summary>
public class AwsSecretsManager : ISecretsManager
{
    private readonly IAmazonSecretsManager _secretsManagerClient;
    private readonly ILogger<AwsSecretsManager> _logger;
    private readonly string _secretPrefix;

    public AwsSecretsManager(
        IConfiguration configuration,
        ILogger<AwsSecretsManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _secretPrefix = configuration["AwsSecretsManager:SecretPrefix"] ?? string.Empty;

        // Use default credential chain:
        // - IAM roles (for EC2/ECS/Lambda)
        // - Environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
        // - AWS credentials file (~/.aws/credentials)
        // - IAM instance profile
        
        // Get region from configuration or environment variable
        var region = configuration["AWS:Region"] 
            ?? Environment.GetEnvironmentVariable("AWS_REGION") 
            ?? "us-east-1"; // Default region
        
        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        _secretsManagerClient = new AmazonSecretsManagerClient(regionEndpoint);
        
        _logger.LogInformation("AWS Secrets Manager initialized");
    }

    private string GetSecretName(string key)
    {
        // Convert configuration key format to AWS secret name
        // e.g., "JwtSettings:SecretKey" -> "Payment/JwtSettings/SecretKey" or "Payment-JwtSettings-SecretKey"
        var normalizedKey = key.Replace(":", "/");
        
        if (!string.IsNullOrEmpty(_secretPrefix))
        {
            return $"{_secretPrefix}/{normalizedKey}";
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
            var secretName = GetSecretName(key);
            var request = new GetSecretValueRequest
            {
                SecretId = secretName
            };

            var response = await _secretsManagerClient.GetSecretValueAsync(request, cancellationToken);
            
            _logger.LogDebug("Retrieved secret for key '{Key}' from AWS Secrets Manager", key);
            return response.SecretString;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Secret key '{Key}' not found in AWS Secrets Manager", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret '{Key}' from AWS Secrets Manager", key);
            throw;
        }
    }

    public async Task<string> GetSecretRequiredAsync(string key, CancellationToken cancellationToken = default)
    {
        var secret = await GetSecretAsync(key, cancellationToken);
        
        if (string.IsNullOrEmpty(secret))
        {
            throw new KeyNotFoundException($"Required secret '{key}' not found in AWS Secrets Manager");
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
            var secretName = GetSecretName(key);
            
            // Check if secret exists
            var exists = await SecretExistsAsync(key, cancellationToken);
            
            if (exists)
            {
                // Update existing secret
                var updateRequest = new UpdateSecretRequest
                {
                    SecretId = secretName,
                    SecretString = value
                };
                await _secretsManagerClient.UpdateSecretAsync(updateRequest, cancellationToken);
                _logger.LogInformation("Secret '{Key}' updated in AWS Secrets Manager", key);
            }
            else
            {
                // Create new secret
                var createRequest = new CreateSecretRequest
                {
                    Name = secretName,
                    SecretString = value
                };
                await _secretsManagerClient.CreateSecretAsync(createRequest, cancellationToken);
                _logger.LogInformation("Secret '{Key}' created in AWS Secrets Manager", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting secret '{Key}' in AWS Secrets Manager", key);
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
            var secretName = GetSecretName(key);
            var request = new DescribeSecretRequest
            {
                SecretId = secretName
            };

            await _secretsManagerClient.DescribeSecretAsync(request, cancellationToken);
            
            _logger.LogDebug("Secret key '{Key}' exists in AWS Secrets Manager", key);
            return true;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogDebug("Secret key '{Key}' does not exist in AWS Secrets Manager", key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if secret '{Key}' exists in AWS Secrets Manager", key);
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
            var secretName = GetSecretName(key);
            var request = new DeleteSecretRequest
            {
                SecretId = secretName,
                ForceDeleteWithoutRecovery = true // Permanently delete
            };

            await _secretsManagerClient.DeleteSecretAsync(request, cancellationToken);
            
            _logger.LogInformation("Secret '{Key}' deleted from AWS Secrets Manager", key);
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Secret key '{Key}' not found in AWS Secrets Manager for deletion", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting secret '{Key}' from AWS Secrets Manager", key);
            throw;
        }
    }
}

