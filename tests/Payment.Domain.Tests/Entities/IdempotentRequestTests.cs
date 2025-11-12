using FluentAssertions;
using Payment.Domain.Entities;
using Xunit;

namespace Payment.Domain.Tests.Entities;

public class IdempotentRequestTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var idempotencyKey = "test-idempotency-key-12345";
        var paymentId = Guid.NewGuid();
        var requestHash = "abc123def456";
        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.AddHours(24);

        // Act
        var idempotentRequest = new IdempotentRequest(
            idempotencyKey,
            paymentId,
            requestHash,
            createdAt,
            expiresAt);

        // Assert
        idempotentRequest.IdempotencyKey.Should().Be(idempotencyKey);
        idempotentRequest.PaymentId.Should().Be(paymentId);
        idempotentRequest.RequestHash.Should().Be(requestHash);
        idempotentRequest.CreatedAt.Should().Be(createdAt);
        idempotentRequest.ExpiresAt.Should().Be(expiresAt);
        idempotentRequest.IsExpired.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrEmptyIdempotencyKey_ShouldThrowArgumentException(string? key)
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var requestHash = "abc123";
        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.AddHours(24);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IdempotentRequest(
            key!, paymentId, requestHash, createdAt, expiresAt));
    }

    [Fact]
    public void Constructor_WithEmptyPaymentId_ShouldThrowArgumentException()
    {
        // Arrange
        var idempotencyKey = "test-key";
        var paymentId = Guid.Empty;
        var requestHash = "abc123";
        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.AddHours(24);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IdempotentRequest(
            idempotencyKey, paymentId, requestHash, createdAt, expiresAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrEmptyRequestHash_ShouldThrowArgumentException(string? hash)
    {
        // Arrange
        var idempotencyKey = "test-key";
        var paymentId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.AddHours(24);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IdempotentRequest(
            idempotencyKey, paymentId, hash!, createdAt, expiresAt));
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsInPast_ShouldReturnTrue()
    {
        // Arrange
        var idempotencyKey = "test-key";
        var paymentId = Guid.NewGuid();
        var requestHash = "abc123";
        var createdAt = DateTime.UtcNow.AddHours(-25);
        var expiresAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var idempotentRequest = new IdempotentRequest(
            idempotencyKey,
            paymentId,
            requestHash,
            createdAt,
            expiresAt);

        // Assert
        idempotentRequest.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsInFuture_ShouldReturnFalse()
    {
        // Arrange
        var idempotencyKey = "test-key";
        var paymentId = Guid.NewGuid();
        var requestHash = "abc123";
        var createdAt = DateTime.UtcNow;
        var expiresAt = DateTime.UtcNow.AddHours(24);

        // Act
        var idempotentRequest = new IdempotentRequest(
            idempotencyKey,
            paymentId,
            requestHash,
            createdAt,
            expiresAt);

        // Assert
        idempotentRequest.IsExpired.Should().BeFalse();
    }
}


