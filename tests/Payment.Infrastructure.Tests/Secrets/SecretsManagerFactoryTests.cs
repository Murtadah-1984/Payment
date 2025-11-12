using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Secrets;
using Xunit;

namespace Payment.Infrastructure.Tests.Secrets;

public class SecretsManagerFactoryTests
{
    [Theory]
    [InlineData("configuration", typeof(ConfigurationSecretsManager))]
    [InlineData("Configuration", typeof(ConfigurationSecretsManager))]
    [InlineData("CONFIGURATION", typeof(ConfigurationSecretsManager))]
    [InlineData("", typeof(ConfigurationSecretsManager))] // Default
    [InlineData(null, typeof(ConfigurationSecretsManager))] // Default
    public void AddSecretsManagement_WithConfigurationProvider_RegistersConfigurationSecretsManager(
        string? provider,
        Type expectedType)
    {
        // Arrange
        var services = new ServiceCollection();
        var configurationBuilder = new ConfigurationBuilder();
        
        if (provider != null)
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SecretsManagement:Provider", provider }
            });
        }
        
        var configuration = configurationBuilder.Build();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddLogging();

        // Act
        services.AddSecretsManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var secretsManager = serviceProvider.GetRequiredService<ISecretsManager>();
        secretsManager.Should().BeOfType(expectedType);
    }

    [Fact]
    public void AddSecretsManagement_WithAzureProvider_RegistersAzureKeyVaultSecretsManager()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SecretsManagement:Provider", "azure-keyvault" },
            { "KeyVault:Uri", "https://test-keyvault.vault.azure.net/" }
        });
        
        var configuration = configurationBuilder.Build();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddLogging();

        // Act
        services.AddSecretsManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var secretsManager = serviceProvider.GetRequiredService<ISecretsManager>();
        secretsManager.Should().BeOfType<AzureKeyVaultSecretsManager>();
    }

    [Fact]
    public void AddSecretsManagement_WithAwsProvider_RegistersAwsSecretsManager()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SecretsManagement:Provider", "aws-secretsmanager" },
            { "AWS:Region", "us-east-1" } // Required for AWS client
        });
        
        var configuration = configurationBuilder.Build();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddLogging();

        // Act
        services.AddSecretsManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var secretsManager = serviceProvider.GetRequiredService<ISecretsManager>();
        secretsManager.Should().BeOfType<AwsSecretsManager>();
    }

    [Fact]
    public void AddSecretsManagement_WithKubernetesProvider_RegistersConfigurationSecretsManager()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SecretsManagement:Provider", "kubernetes" }
        });
        
        var configuration = configurationBuilder.Build();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddLogging();

        // Act
        services.AddSecretsManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var secretsManager = serviceProvider.GetRequiredService<ISecretsManager>();
        secretsManager.Should().BeOfType<ConfigurationSecretsManager>();
    }
}

