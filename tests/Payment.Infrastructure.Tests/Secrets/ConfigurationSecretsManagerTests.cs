using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Infrastructure.Secrets;
using Xunit;

namespace Payment.Infrastructure.Tests.Secrets;

public class ConfigurationSecretsManagerTests
{
    private readonly Mock<ILogger<ConfigurationSecretsManager>> _loggerMock;
    private readonly IConfiguration _configuration;

    public ConfigurationSecretsManagerTests()
    {
        _loggerMock = new Mock<ILogger<ConfigurationSecretsManager>>();
        
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "JwtSettings:SecretKey", "test-secret-key-12345" },
            { "PaymentProviders:ZainCash:MerchantSecret", "zaincash-secret" },
            { "EmptyKey", "" },
            { "NullKey", null }
        });
        
        _configuration = configurationBuilder.Build();
    }

    [Fact]
    public async Task GetSecretAsync_WhenSecretExists_ReturnsSecret()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act
        var result = await manager.GetSecretAsync("JwtSettings:SecretKey");

        // Assert
        result.Should().Be("test-secret-key-12345");
    }

    [Fact]
    public async Task GetSecretAsync_WhenSecretDoesNotExist_ReturnsNull()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act
        var result = await manager.GetSecretAsync("NonExistent:Key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSecretAsync_WhenKeyIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => manager.GetSecretAsync(""));
    }

    [Fact]
    public async Task GetSecretAsync_WhenKeyIsNull_ThrowsArgumentException()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => manager.GetSecretAsync(null!));
    }

    [Fact]
    public async Task GetSecretRequiredAsync_WhenSecretExists_ReturnsSecret()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act
        var result = await manager.GetSecretRequiredAsync("JwtSettings:SecretKey");

        // Assert
        result.Should().Be("test-secret-key-12345");
    }

    [Fact]
    public async Task GetSecretRequiredAsync_WhenSecretDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => manager.GetSecretRequiredAsync("NonExistent:Key"));
    }

    [Fact]
    public async Task SetSecretAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            () => manager.SetSecretAsync("Key", "Value"));
    }

    [Fact]
    public async Task SecretExistsAsync_WhenSecretExists_ReturnsTrue()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act
        var result = await manager.SecretExistsAsync("JwtSettings:SecretKey");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SecretExistsAsync_WhenSecretDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act
        var result = await manager.SecretExistsAsync("NonExistent:Key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SecretExistsAsync_WhenSecretIsEmpty_ReturnsFalse()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act
        var result = await manager.SecretExistsAsync("EmptyKey");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSecretAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var manager = new ConfigurationSecretsManager(_configuration, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            () => manager.DeleteSecretAsync("Key"));
    }
}

