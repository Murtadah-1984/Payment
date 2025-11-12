using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

public class IdempotencyKeyTests
{
    [Fact]
    public void Constructor_WithValidKey_ShouldCreateInstance()
    {
        // Arrange
        var key = "valid-idempotency-key-12345";

        // Act
        var idempotencyKey = new IdempotencyKey(key);

        // Assert
        idempotencyKey.Value.Should().Be(key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrEmptyKey_ShouldThrowArgumentException(string? key)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IdempotencyKey(key!));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("123456789012345")] // 15 characters
    public void Constructor_WithKeyShorterThan16Characters_ShouldThrowArgumentException(string key)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new IdempotencyKey(key));
        exception.Message.Should().Contain("16");
    }

    [Fact]
    public void Constructor_WithKeyLongerThan128Characters_ShouldThrowArgumentException()
    {
        // Arrange
        var key = new string('a', 129); // 129 characters

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new IdempotencyKey(key));
        exception.Message.Should().Contain("128");
    }

    [Fact]
    public void Constructor_WithKeyExactly16Characters_ShouldCreateInstance()
    {
        // Arrange
        var key = new string('a', 16);

        // Act
        var idempotencyKey = new IdempotencyKey(key);

        // Assert
        idempotencyKey.Value.Should().Be(key);
    }

    [Fact]
    public void Constructor_WithKeyExactly128Characters_ShouldCreateInstance()
    {
        // Arrange
        var key = new string('a', 128);

        // Act
        var idempotencyKey = new IdempotencyKey(key);

        // Assert
        idempotencyKey.Value.Should().Be(key);
    }

    [Fact]
    public void ImplicitConversion_ShouldConvertToString()
    {
        // Arrange
        var key = "test-idempotency-key-123";
        var idempotencyKey = new IdempotencyKey(key);

        // Act
        string result = idempotencyKey;

        // Assert
        result.Should().Be(key);
    }
}


