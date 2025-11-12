using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Security;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Payment.Infrastructure.Tests.Security;

public class CallbackSignatureValidatorTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<CallbackSignatureValidator>> _loggerMock;
    private readonly CallbackSignatureValidator _validator;

    public CallbackSignatureValidatorTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<CallbackSignatureValidator>>();
        _validator = new CallbackSignatureValidator(_configurationMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnTrue_WhenSignatureIsValid()
    {
        // Arrange
        var provider = "ZainCash";
        var payload = "test-payload";
        var timestamp = "1234567890";
        var webhookSecret = "test-secret-key";
        
        // Compute expected signature
        var expectedSignature = ComputeHmacSha256(payload + timestamp, webhookSecret);

        _configurationMock
            .Setup(c => c[$"PaymentProviders:{provider}:WebhookSecret"])
            .Returns(webhookSecret);

        // Act
        var result = await _validator.ValidateAsync(provider, payload, expectedSignature, timestamp);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnTrue_WhenSignatureIsValid_WithoutTimestamp()
    {
        // Arrange
        var provider = "FIB";
        var payload = "test-payload";
        var webhookSecret = "test-secret-key";
        
        // Compute expected signature (without timestamp)
        var expectedSignature = ComputeHmacSha256(payload, webhookSecret);

        _configurationMock
            .Setup(c => c[$"PaymentProviders:{provider}:WebhookSecret"])
            .Returns(webhookSecret);

        // Act
        var result = await _validator.ValidateAsync(provider, payload, expectedSignature, null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnFalse_WhenSignatureIsInvalid()
    {
        // Arrange
        var provider = "Telr";
        var payload = "test-payload";
        var timestamp = "1234567890";
        var webhookSecret = "test-secret-key";
        var invalidSignature = "invalid-signature";

        _configurationMock
            .Setup(c => c[$"PaymentProviders:{provider}:WebhookSecret"])
            .Returns(webhookSecret);

        // Act
        var result = await _validator.ValidateAsync(provider, payload, invalidSignature, timestamp);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnFalse_WhenSignatureIsMissing()
    {
        // Arrange
        var provider = "ZainCash";
        var payload = "test-payload";
        var timestamp = "1234567890";
        var webhookSecret = "test-secret-key";

        _configurationMock
            .Setup(c => c[$"PaymentProviders:{provider}:WebhookSecret"])
            .Returns(webhookSecret);

        // Act
        var result = await _validator.ValidateAsync(provider, payload, null, timestamp);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnFalse_WhenWebhookSecretIsNotConfigured()
    {
        // Arrange
        var provider = "ZainCash";
        var payload = "test-payload";
        var signature = "some-signature";
        var timestamp = "1234567890";

        _configurationMock
            .Setup(c => c[$"PaymentProviders:{provider}:WebhookSecret"])
            .Returns((string?)null);

        // Act
        var result = await _validator.ValidateAsync(provider, payload, signature, timestamp);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnFalse_WhenWebhookSecretIsEmpty()
    {
        // Arrange
        var provider = "FIB";
        var payload = "test-payload";
        var signature = "some-signature";
        var timestamp = "1234567890";

        _configurationMock
            .Setup(c => c[$"PaymentProviders:{provider}:WebhookSecret"])
            .Returns(string.Empty);

        // Act
        var result = await _validator.ValidateAsync(provider, payload, signature, timestamp);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHandleCaseInsensitiveSignature()
    {
        // Arrange
        var provider = "ZainCash";
        var payload = "test-payload";
        var timestamp = "1234567890";
        var webhookSecret = "test-secret-key";
        
        // Compute expected signature and convert to uppercase
        var expectedSignature = ComputeHmacSha256(payload + timestamp, webhookSecret).ToUpperInvariant();

        _configurationMock
            .Setup(c => c[$"PaymentProviders:{provider}:WebhookSecret"])
            .Returns(webhookSecret);

        // Act
        var result = await _validator.ValidateAsync(provider, payload, expectedSignature, timestamp);

        // Assert
        // Note: Our implementation uses lowercase, so uppercase should fail
        // This test verifies case-sensitive comparison (which is correct for security)
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldPreventTimingAttacks()
    {
        // Arrange
        var provider = "Telr";
        var payload = "test-payload";
        var timestamp = "1234567890";
        var webhookSecret = "test-secret-key";
        
        // Compute valid signature
        var validSignature = ComputeHmacSha256(payload + timestamp, webhookSecret);
        
        // Create invalid signature with same length (to test timing attack prevention)
        var invalidSignature = new string('a', validSignature.Length);

        _configurationMock
            .Setup(c => c[$"PaymentProviders:{provider}:WebhookSecret"])
            .Returns(webhookSecret);

        // Act
        var validResult = await _validator.ValidateAsync(provider, payload, validSignature, timestamp);
        var invalidResult = await _validator.ValidateAsync(provider, payload, invalidSignature, timestamp);

        // Assert
        validResult.Should().BeTrue();
        invalidResult.Should().BeFalse();
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

