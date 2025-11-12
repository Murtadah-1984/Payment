using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Infrastructure.Data;
using Payment.Infrastructure.Security;
using Xunit;

namespace Payment.Infrastructure.Tests.Security;

/// <summary>
/// Unit tests for CredentialRevocationService.
/// Tests cover credential revocation, checking revocation status, and secret rotation.
/// </summary>
public class CredentialRevocationServiceTests : IDisposable
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<CredentialRevocationService>> _loggerMock;
    private readonly PaymentDbContext _dbContext;
    private readonly CredentialRevocationService _service;

    public CredentialRevocationServiceTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<CredentialRevocationService>>();

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PaymentDbContext(options);
        _service = new CredentialRevocationService(
            _cacheMock.Object,
            _dbContext,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_ShouldRevokeCredential_WhenValidApiKeyId()
    {
        // Arrange
        var apiKeyId = "api-key-123";
        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _cacheMock.Setup(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RevokeApiKeyAsync(apiKeyId, CancellationToken.None);

        // Assert
        var revoked = await _dbContext.RevokedCredentials
            .FirstOrDefaultAsync(rc => rc.CredentialId == apiKeyId);
        revoked.Should().NotBeNull();
        revoked!.Type.Should().Be(CredentialType.ApiKey);
        revoked.Reason.Should().Contain("Revoked via API");
        _cacheMock.Verify(c => c.SetStringAsync(
            It.Is<string>(k => k.Contains(apiKeyId)),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeJwtTokenAsync_ShouldRevokeCredential_WhenValidTokenId()
    {
        // Arrange
        var tokenId = "jwt-token-456";
        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _cacheMock.Setup(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RevokeJwtTokenAsync(tokenId, CancellationToken.None);

        // Assert
        var revoked = await _dbContext.RevokedCredentials
            .FirstOrDefaultAsync(rc => rc.CredentialId == tokenId);
        revoked.Should().NotBeNull();
        revoked!.Type.Should().Be(CredentialType.JwtToken);
        revoked.ExpiresAt.Should().NotBeNull(); // JWT tokens should have expiration
    }

    [Fact]
    public async Task IsRevokedAsync_ShouldReturnTrue_WhenCredentialIsInCache()
    {
        // Arrange
        var credentialId = "revoked-credential";
        _cacheMock.Setup(c => c.GetStringAsync(
            It.Is<string>(k => k.Contains(credentialId)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"CredentialId\":\"revoked-credential\"}");

        // Act
        var result = await _service.IsRevokedAsync(credentialId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRevokedAsync_ShouldReturnTrue_WhenCredentialIsInDatabase()
    {
        // Arrange
        var credentialId = "db-revoked-credential";
        var revokedCredential = new RevokedCredential
        {
            CredentialId = credentialId,
            Type = CredentialType.ApiKey,
            RevokedAt = DateTime.UtcNow,
            Reason = "Test revocation"
        };
        _dbContext.RevokedCredentials.Add(revokedCredential);
        await _dbContext.SaveChangesAsync();

        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.IsRevokedAsync(credentialId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRevokedAsync_ShouldReturnFalse_WhenCredentialIsNotRevoked()
    {
        // Arrange
        var credentialId = "valid-credential";
        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.IsRevokedAsync(credentialId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RotateSecretsAsync_ShouldRevokeOldSecret_WhenValidSecretName()
    {
        // Arrange
        var secretName = "database-connection-string";
        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _cacheMock.Setup(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RotateSecretsAsync(secretName, CancellationToken.None);

        // Assert
        var revoked = await _dbContext.RevokedCredentials
            .FirstOrDefaultAsync(rc => rc.CredentialId == secretName);
        revoked.Should().NotBeNull();
        revoked!.Type.Should().Be(CredentialType.DatabaseConnection);
        revoked.Reason.Should().Contain("Secret rotation");
    }

    [Fact]
    public async Task GetRevokedCredentialsAsync_ShouldReturnAllRevokedCredentials_WhenNoFilter()
    {
        // Arrange
        var credential1 = new RevokedCredential
        {
            CredentialId = "cred-1",
            Type = CredentialType.ApiKey,
            RevokedAt = DateTime.UtcNow.AddDays(-1),
            Reason = "Test 1"
        };
        var credential2 = new RevokedCredential
        {
            CredentialId = "cred-2",
            Type = CredentialType.JwtToken,
            RevokedAt = DateTime.UtcNow,
            Reason = "Test 2"
        };
        _dbContext.RevokedCredentials.AddRange(credential1, credential2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRevokedCredentialsAsync(null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(rc => rc.CredentialId == "cred-1");
        result.Should().Contain(rc => rc.CredentialId == "cred-2");
    }

    [Fact]
    public async Task GetRevokedCredentialsAsync_ShouldFilterByDate_WhenSinceProvided()
    {
        // Arrange
        var oldCredential = new RevokedCredential
        {
            CredentialId = "old-cred",
            Type = CredentialType.ApiKey,
            RevokedAt = DateTime.UtcNow.AddDays(-10),
            Reason = "Old"
        };
        var newCredential = new RevokedCredential
        {
            CredentialId = "new-cred",
            Type = CredentialType.JwtToken,
            RevokedAt = DateTime.UtcNow,
            Reason = "New"
        };
        _dbContext.RevokedCredentials.AddRange(oldCredential, newCredential);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRevokedCredentialsAsync(
            DateTime.UtcNow.AddDays(-1),
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(rc => rc.CredentialId == "new-cred");
        result.Should().NotContain(rc => rc.CredentialId == "old-cred");
    }

    [Fact]
    public async Task RevokeApiKeyAsync_ShouldThrowException_WhenApiKeyIdIsEmpty()
    {
        // Arrange & Act
        var act = async () => await _service.RevokeApiKeyAsync(string.Empty, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_ShouldNotDuplicate_WhenAlreadyRevoked()
    {
        // Arrange
        var apiKeyId = "duplicate-key";
        var existing = new RevokedCredential
        {
            CredentialId = apiKeyId,
            Type = CredentialType.ApiKey,
            RevokedAt = DateTime.UtcNow.AddHours(-1),
            Reason = "Already revoked"
        };
        _dbContext.RevokedCredentials.Add(existing);
        await _dbContext.SaveChangesAsync();

        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        await _service.RevokeApiKeyAsync(apiKeyId, CancellationToken.None);

        // Assert
        var revoked = await _dbContext.RevokedCredentials
            .Where(rc => rc.CredentialId == apiKeyId)
            .ToListAsync();
        revoked.Should().HaveCount(1); // Should not create duplicate
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

