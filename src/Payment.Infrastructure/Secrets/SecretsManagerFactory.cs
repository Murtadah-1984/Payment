using Amazon.SecretsManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Secrets;

/// <summary>
/// Factory for creating the appropriate secrets manager based on configuration.
/// Follows Factory Pattern and Strategy Pattern.
/// </summary>
public static class SecretsManagerFactory
{
    /// <summary>
    /// Creates and registers the appropriate secrets manager based on configuration.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddSecretsManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var secretsProvider = configuration["SecretsManagement:Provider"]?.ToLowerInvariant() ?? "configuration";

        switch (secretsProvider)
        {
            case "azure-keyvault":
            case "azure":
                services.AddSingleton<ISecretsManager>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<AzureKeyVaultSecretsManager>>();
                    return new AzureKeyVaultSecretsManager(configuration, logger);
                });
                break;

            case "aws-secretsmanager":
            case "aws":
                services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
                services.AddSingleton<ISecretsManager>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<AwsSecretsManager>>();
                    return new AwsSecretsManager(configuration, logger);
                });
                break;

            case "kubernetes":
            case "k8s":
                // Kubernetes secrets are typically loaded via environment variables or mounted files
                // For simplicity, we use ConfigurationSecretsManager which reads from environment variables
                // In production, use External Secrets Operator to sync secrets from Key Vault/AWS to K8s
                services.AddSingleton<ISecretsManager>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<ConfigurationSecretsManager>>();
                    return new ConfigurationSecretsManager(configuration, logger);
                });
                break;

            case "configuration":
            default:
                // Development/fallback: use configuration (appsettings.json, environment variables)
                services.AddSingleton<ISecretsManager>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<ConfigurationSecretsManager>>();
                    return new ConfigurationSecretsManager(configuration, logger);
                });
                break;
        }

        return services;
    }
}

