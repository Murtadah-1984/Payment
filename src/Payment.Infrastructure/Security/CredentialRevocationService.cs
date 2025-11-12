using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Data;
using System.Text.Json;

namespace Payment.Infrastructure.Security;

/// <summary>
/// Service for revoking compromised credentials.
/// Follows Single Responsibility Principle - only handles credential revocation.
/// Stateless by design - suitable for Kubernetes deployment.
/// Uses distributed cache (Redis) for fast revocation checks and database for audit trail.
/// </summary>
public class CredentialRevocationService : ICredentialRevocationService
{
    private readonly IDistributedCache _distributedCache;
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<CredentialRevocationService> _logger;
    private const string RevocationKeyPrefix = "revoked:";

    public CredentialRevocationService(
        IDistributedCache distributedCache,
        PaymentDbContext dbContext,
        ILogger<CredentialRevocationService> logger)
    {
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RevokeApiKeyAsync(
        string apiKeyId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKeyId))
            throw new ArgumentException("API key ID cannot be null or empty", nameof(apiKeyId));

        _logger.LogInformation("Revoking API key. ApiKeyId: {ApiKeyId}", apiKeyId);

        await RevokeCredentialAsync(
            apiKeyId,
            CredentialType.ApiKey,
            "Revoked via API",
            cancellationToken);
    }

    public async Task RevokeJwtTokenAsync(
        string tokenId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
            throw new ArgumentException("Token ID cannot be null or empty", nameof(tokenId));

        _logger.LogInformation("Revoking JWT token. TokenId: {TokenId}", tokenId);

        await RevokeCredentialAsync(
            tokenId,
            CredentialType.JwtToken,
            "Revoked via API",
            cancellationToken);
    }

    public async Task RotateSecretsAsync(
        string secretName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
            throw new ArgumentException("Secret name cannot be null or empty", nameof(secretName));

        _logger.LogInformation("Rotating secret. SecretName: {SecretName}", secretName);

        // Determine credential type based on secret name
        var credentialType = DetermineCredentialTypeFromSecretName(secretName);

        // Revoke the old secret
        await RevokeCredentialAsync(
            secretName,
            credentialType,
            "Secret rotation",
            cancellationToken);

        // Note: Actual secret rotation would be handled by secrets manager
        // This service only handles the revocation of the old secret
        _logger.LogInformation(
            "Secret rotation initiated. SecretName: {SecretName}, Type: {Type}",
            secretName, credentialType);
    }

    public async Task<bool> IsRevokedAsync(
        string credentialId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
            throw new ArgumentException("Credential ID cannot be null or empty", nameof(credentialId));

        // First check cache for fast lookup
        var key = GetRevocationKey(credentialId);
        var revokedData = await _distributedCache.GetStringAsync(key, cancellationToken);

        if (!string.IsNullOrEmpty(revokedData))
            return true;

        // Fallback to database check
        var isRevoked = await _dbContext.RevokedCredentials
            .AnyAsync(rc => rc.CredentialId == credentialId, cancellationToken);

        return isRevoked;
    }

    public async Task<IEnumerable<RevokedCredential>> GetRevokedCredentialsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RevokedCredentials.AsQueryable();

        if (since.HasValue)
        {
            query = query.Where(rc => rc.RevokedAt >= since.Value);
        }

        var revokedCredentials = await query
            .OrderByDescending(rc => rc.RevokedAt)
            .ToListAsync(cancellationToken);

        return revokedCredentials;
    }

    private async Task RevokeCredentialAsync(
        string credentialId,
        CredentialType credentialType,
        string reason,
        CancellationToken cancellationToken)
    {
        // Check if already revoked
        var existing = await _dbContext.RevokedCredentials
            .FirstOrDefaultAsync(rc => rc.CredentialId == credentialId, cancellationToken);

        if (existing != null)
        {
            _logger.LogWarning(
                "Credential already revoked. CredentialId: {CredentialId}",
                credentialId);
            return;
        }

        // Create revoked credential entity
        var revokedCredential = new RevokedCredential
        {
            CredentialId = credentialId,
            Type = credentialType,
            RevokedAt = DateTime.UtcNow,
            Reason = reason,
            RevokedBy = "System", // TODO: Get from current user context
            ExpiresAt = credentialType == CredentialType.JwtToken 
                ? DateTime.UtcNow.AddDays(30) // JWT tokens typically expire
                : null
        };

        // Store in database for audit trail
        _dbContext.RevokedCredentials.Add(revokedCredential);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Store in cache for fast lookup
        var key = GetRevocationKey(credentialId);
        var revocationData = new
        {
            CredentialId = credentialId,
            CredentialType = credentialType.ToString(),
            Reason = reason,
            RevokedAt = revokedCredential.RevokedAt
        };

        var json = JsonSerializer.Serialize(revocationData);
        
        // Store in cache with TTL based on credential type
        var ttl = credentialType switch
        {
            CredentialType.JwtToken => TimeSpan.FromDays(30),
            CredentialType.ApiKey => TimeSpan.FromDays(90),
            _ => TimeSpan.FromDays(30)
        };

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        await _distributedCache.SetStringAsync(key, json, options, cancellationToken);

        _logger.LogInformation(
            "Credential revoked. CredentialId: {CredentialId}, Type: {CredentialType}, Reason: {Reason}",
            credentialId, credentialType, reason);
    }

    private static CredentialType DetermineCredentialTypeFromSecretName(string secretName)
    {
        var name = secretName.ToLowerInvariant();

        if (name.Contains("database") || name.Contains("connection"))
            return CredentialType.DatabaseConnection;
        
        if (name.Contains("payment") && name.Contains("provider"))
            return CredentialType.PaymentProviderKey;
        
        if (name.Contains("jwt") && name.Contains("signing"))
            return CredentialType.JwtSigningKey;
        
        if (name.Contains("webhook"))
            return CredentialType.WebhookSecret;

        return CredentialType.Other;
    }

    private static string GetRevocationKey(string credentialId)
    {
        return $"{RevocationKeyPrefix}{credentialId}";
    }
}

