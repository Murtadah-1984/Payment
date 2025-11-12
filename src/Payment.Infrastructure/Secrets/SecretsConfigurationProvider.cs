using Microsoft.Extensions.Configuration;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Secrets;

/// <summary>
/// Configuration provider that loads secrets from ISecretsManager.
/// Integrates secrets management with ASP.NET Core configuration system.
/// </summary>
public class SecretsConfigurationProvider : ConfigurationProvider
{
    private readonly ISecretsManager _secretsManager;
    private readonly string[] _secretKeys;

    public SecretsConfigurationProvider(ISecretsManager secretsManager, string[] secretKeys)
    {
        _secretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
        _secretKeys = secretKeys ?? throw new ArgumentNullException(nameof(secretKeys));
    }

    public override void Load()
    {
        var cancellationToken = CancellationToken.None;

        foreach (var key in _secretKeys)
        {
            try
            {
                var secret = _secretsManager.GetSecretAsync(key, cancellationToken).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(secret))
                {
                    Data[key] = secret;
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - configuration provider should be resilient
                // In production, use proper logging
                System.Diagnostics.Debug.WriteLine($"Error loading secret '{key}': {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Configuration source for secrets management.
/// </summary>
public class SecretsConfigurationSource : IConfigurationSource
{
    private readonly ISecretsManager _secretsManager;
    private readonly string[] _secretKeys;

    public SecretsConfigurationSource(ISecretsManager secretsManager, string[] secretKeys)
    {
        _secretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
        _secretKeys = secretKeys ?? throw new ArgumentNullException(nameof(secretKeys));
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new SecretsConfigurationProvider(_secretsManager, _secretKeys);
    }
}

/// <summary>
/// Extension methods for adding secrets to configuration.
/// </summary>
public static class SecretsConfigurationExtensions
{
    /// <summary>
    /// Adds secrets from ISecretsManager to the configuration.
    /// </summary>
    /// <param name="builder">Configuration builder</param>
    /// <param name="secretKeys">Array of secret keys to load</param>
    /// <returns>Configuration builder for chaining</returns>
    public static IConfigurationBuilder AddSecrets(
        this IConfigurationBuilder builder,
        ISecretsManager secretsManager,
        params string[] secretKeys)
    {
        return builder.Add(new SecretsConfigurationSource(secretsManager, secretKeys));
    }
}

