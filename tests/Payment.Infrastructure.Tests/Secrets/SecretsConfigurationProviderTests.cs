using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Secrets;
using Xunit;

namespace Payment.Infrastructure.Tests.Secrets;

public class SecretsConfigurationProviderTests
{
    [Fact]
    public void Load_WhenSecretsExist_LoadsSecretsIntoData()
    {
        // Arrange
        var secretsManagerMock = new Mock<ISecretsManager>();
        secretsManagerMock.Setup(m => m.GetSecretAsync("Key1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Value1");
        secretsManagerMock.Setup(m => m.GetSecretAsync("Key2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Value2");

        var provider = new SecretsConfigurationProvider(
            secretsManagerMock.Object,
            new[] { "Key1", "Key2" });

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Key1", out var value1).Should().BeTrue();
        value1.Should().Be("Value1");
        
        provider.TryGet("Key2", out var value2).Should().BeTrue();
        value2.Should().Be("Value2");
    }

    [Fact]
    public void Load_WhenSecretDoesNotExist_DoesNotAddToData()
    {
        // Arrange
        var secretsManagerMock = new Mock<ISecretsManager>();
        secretsManagerMock.Setup(m => m.GetSecretAsync("Key1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var provider = new SecretsConfigurationProvider(
            secretsManagerMock.Object,
            new[] { "Key1" });

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Key1", out _).Should().BeFalse();
    }

    [Fact]
    public void Load_WhenExceptionOccurs_ContinuesLoadingOtherSecrets()
    {
        // Arrange
        var secretsManagerMock = new Mock<ISecretsManager>();
        secretsManagerMock.Setup(m => m.GetSecretAsync("Key1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error"));
        secretsManagerMock.Setup(m => m.GetSecretAsync("Key2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Value2");

        var provider = new SecretsConfigurationProvider(
            secretsManagerMock.Object,
            new[] { "Key1", "Key2" });

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Key1", out _).Should().BeFalse();
        provider.TryGet("Key2", out var value2).Should().BeTrue();
        value2.Should().Be("Value2");
    }

    [Fact]
    public void AddSecrets_ExtensionMethod_AddsSecretsConfigurationSource()
    {
        // Arrange
        var configurationBuilder = new ConfigurationBuilder();
        var secretsManagerMock = new Mock<ISecretsManager>();
        secretsManagerMock.Setup(m => m.GetSecretAsync("Key1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Value1");

        // Act
        configurationBuilder.AddSecrets(secretsManagerMock.Object, "Key1");
        var configuration = configurationBuilder.Build();

        // Assert
        configuration["Key1"].Should().Be("Value1");
    }
}

