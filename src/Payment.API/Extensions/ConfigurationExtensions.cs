using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Secrets;

namespace Payment.API.Extensions;

/// <summary>
/// Extension methods for configuration with secrets management.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Adds secrets management to configuration.
    /// This method creates a temporary service provider to get ISecretsManager,
    /// then loads secrets into the configuration.
    /// </summary>
    public static IConfigurationBuilder AddSecretsManagement(
        this IConfigurationBuilder builder,
        IConfiguration configuration)
    {
        // Determine which secrets provider to use
        var secretsProvider = configuration["SecretsManagement:Provider"]?.ToLowerInvariant() ?? "configuration";

        // For development/configuration provider, secrets are already in appsettings.json
        // For production (Azure Key Vault, AWS Secrets Manager), we need to load them
        if (secretsProvider != "configuration" && secretsProvider != "kubernetes" && secretsProvider != "k8s")
        {
            // Define secret keys that should be loaded from secrets manager
            // Note: JWT authentication is now handled by external Identity Microservice
            // No local JWT secret key is required
            var secretKeys = new[]
            {
                "ConnectionStrings:DefaultConnection",
                "DataEncryption:Key",
                // Payment provider secrets (load dynamically based on configuration)
                "PaymentProviders:ZainCash:MerchantSecret",
                "PaymentProviders:ZainCash:WebhookSecret",
                "PaymentProviders:FIB:ClientSecret",
                "PaymentProviders:FIB:WebhookSecret",
                "PaymentProviders:Telr:WebhookSecret",
                "PaymentProviders:Stripe:ApiKey",
                "PaymentProviders:Square:AccessToken",
                "PaymentProviders:Helcim:ApiToken",
                "PaymentProviders:Amazon:AccessCode",
                "PaymentProviders:Amazon:ShaRequestPhrase",
                "PaymentProviders:Amazon:ShaResponsePhrase",
                "PaymentProviders:Checkout:SecretKey",
                "PaymentProviders:Checkout:ClientSecret",
                "PaymentProviders:Verifone:SecretKey",
                "PaymentProviders:Paytabs:ServerKey",
                "PaymentProviders:Tap:SecretKey"
            };

            // Create a temporary service provider to get ISecretsManager
            // This is a workaround since we need ISecretsManager before the full DI container is built
            var tempServices = new ServiceCollection();
            tempServices.AddSingleton<ILoggerFactory, LoggerFactory>();
            tempServices.AddLogging();
            tempServices.AddSecretsManagement(configuration);
            
            using var tempServiceProvider = tempServices.BuildServiceProvider();
            var secretsManager = tempServiceProvider.GetRequiredService<ISecretsManager>();

            // Add secrets to configuration
            builder.AddSecrets(secretsManager, secretKeys);
        }

        return builder;
    }
}

