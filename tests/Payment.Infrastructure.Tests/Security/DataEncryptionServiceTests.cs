using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Payment.Infrastructure.Security;
using Xunit;

namespace Payment.Infrastructure.Tests.Security;

public class DataEncryptionServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly string _validKeyBase64;

    public DataEncryptionServiceTests()
    {
        // Generate a valid 32-byte (256-bit) key for AES-256
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        _validKeyBase64 = Convert.ToBase64String(keyBytes);

        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(c => c["DataEncryption:Key"]).Returns(_validKeyBase64);
        _configuration = configurationMock.Object;
    }

    [Fact]
    public void Constructor_ShouldCreateService_WhenValidKeyIsProvided()
    {
        // Act
        var service = new DataEncryptionService(_configuration);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenKeyIsNotConfigured()
    {
        // Arrange
        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(c => c["DataEncryption:Key"]).Returns((string?)null);

        // Act & Assert
        var act = () => new DataEncryptionService(configurationMock.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DataEncryption:Key not configured*");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenKeyIsInvalidBase64()
    {
        // Arrange
        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(c => c["DataEncryption:Key"]).Returns("invalid-base64!!!");

        // Act & Assert
        var act = () => new DataEncryptionService(configurationMock.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be a valid base64-encoded string*");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenKeyIsWrongSize()
    {
        // Arrange - 16 bytes (128 bits) instead of 32 bytes (256 bits)
        var keyBytes = new byte[16];
        RandomNumberGenerator.Fill(keyBytes);
        var invalidKeyBase64 = Convert.ToBase64String(keyBytes);

        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(c => c["DataEncryption:Key"]).Returns(invalidKeyBase64);

        // Act & Assert
        var act = () => new DataEncryptionService(configurationMock.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be 32 bytes (256 bits) for AES-256*");
    }

    [Fact]
    public void Encrypt_ShouldReturnEncryptedString_WhenPlainTextIsProvided()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);
        var plainText = "This is sensitive payment metadata";

        // Act
        var encrypted = service.Encrypt(plainText);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(plainText);
        encrypted.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$"); // Base64 format
    }

    [Fact]
    public void Encrypt_ShouldReturnEmptyString_WhenPlainTextIsEmpty()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);

        // Act
        var encrypted = service.Encrypt(string.Empty);

        // Assert
        encrypted.Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_ShouldReturnNull_WhenPlainTextIsNull()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);

        // Act
        var encrypted = service.Encrypt(null!);

        // Assert
        encrypted.Should().BeNull();
    }

    [Fact]
    public void Decrypt_ShouldReturnOriginalPlainText_WhenEncryptedStringIsProvided()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);
        var plainText = "This is sensitive payment metadata";

        // Act
        var encrypted = service.Encrypt(plainText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void Decrypt_ShouldReturnEmptyString_WhenEncryptedStringIsEmpty()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);

        // Act
        var decrypted = service.Decrypt(string.Empty);

        // Assert
        decrypted.Should().BeEmpty();
    }

    [Fact]
    public void Decrypt_ShouldReturnNull_WhenEncryptedStringIsNull()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);

        // Act
        var decrypted = service.Decrypt(null!);

        // Assert
        decrypted.Should().BeNull();
    }

    [Fact]
    public void Decrypt_ShouldThrowException_WhenEncryptedStringIsInvalid()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);
        var invalidEncrypted = "invalid-encrypted-data!!!";

        // Act & Assert
        var act = () => service.Decrypt(invalidEncrypted);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_ShouldThrowException_WhenEncryptedStringIsTooShort()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);
        var tooShort = Convert.ToBase64String(new byte[10]); // Less than 16 bytes (IV size)

        // Act & Assert
        var act = () => service.Decrypt(tooShort);
        act.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("Short text")]
    [InlineData("This is a longer piece of sensitive data that needs encryption")]
    [InlineData("{\"key\":\"value\",\"sensitive\":\"data\"}")]
    [InlineData("Special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?")]
    [InlineData("Unicode: 测试 🎉 ñáéíóú")]
    public void EncryptDecrypt_ShouldRoundTrip_ForVariousInputs(string plainText)
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);

        // Act
        var encrypted = service.Encrypt(plainText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void Encrypt_ShouldProduceDifferentOutput_ForSameInput()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);
        var plainText = "Same input text";

        // Act
        var encrypted1 = service.Encrypt(plainText);
        var encrypted2 = service.Encrypt(plainText);

        // Assert
        // Different IVs should produce different encrypted outputs
        encrypted1.Should().NotBe(encrypted2);
        
        // But both should decrypt to the same plain text
        service.Decrypt(encrypted1).Should().Be(plainText);
        service.Decrypt(encrypted2).Should().Be(plainText);
    }

    [Fact]
    public void Decrypt_ShouldThrowException_WhenUsingDifferentKey()
    {
        // Arrange
        var service1 = new DataEncryptionService(_configuration);
        var plainText = "Sensitive data";

        // Generate a different key
        var differentKeyBytes = new byte[32];
        RandomNumberGenerator.Fill(differentKeyBytes);
        var differentKeyBase64 = Convert.ToBase64String(differentKeyBytes);

        var configurationMock2 = new Mock<IConfiguration>();
        configurationMock2.Setup(c => c["DataEncryption:Key"]).Returns(differentKeyBase64);
        var service2 = new DataEncryptionService(configurationMock2.Object);

        // Act
        var encrypted = service1.Encrypt(plainText);

        // Assert - Decrypting with different key should fail
        var act = () => service2.Decrypt(encrypted);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void EncryptDecrypt_ShouldHandleLargeData()
    {
        // Arrange
        var service = new DataEncryptionService(_configuration);
        var largeData = new string('A', 10000); // 10KB of data

        // Act
        var encrypted = service.Encrypt(largeData);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(largeData);
        decrypted.Length.Should().Be(10000);
    }
}

