using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using System.Text.Json;

namespace Payment.Infrastructure.Caching;

/// <summary>
/// Redis-based distributed cache service implementation (Caching Strategy #9).
/// Follows Single Responsibility Principle - only responsible for caching operations.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly IMetricsRecorder? _metricsRecorder;

    public RedisCacheService(
        IDistributedCache cache, 
        ILogger<RedisCacheService> logger,
        IMetricsRecorder? metricsRecorder = null)
    {
        _cache = cache;
        _logger = logger;
        _metricsRecorder = metricsRecorder;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var cached = await _cache.GetStringAsync(key, cancellationToken);
            if (cached == null)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                _metricsRecorder?.RecordCacheOperation("get", "miss");
                return null;
            }

            _logger.LogDebug("Cache hit for key: {Key}", key);
            _metricsRecorder?.RecordCacheOperation("get", "hit");
            return JsonSerializer.Deserialize<T>(cached);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache for key: {Key}", key);
            return null; // Fail gracefully - return null on cache errors
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10)
            };

            var json = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, json, options, cancellationToken);
            _logger.LogDebug("Cached value for key: {Key} with expiry: {Expiry}", key, expiry);
            _metricsRecorder?.RecordCacheOperation("set", "success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache for key: {Key}", key);
            // Fail gracefully - don't throw on cache errors
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _logger.LogDebug("Removed cache key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache for key: {Key}", key);
            // Fail gracefully
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Note: Redis doesn't support pattern-based deletion directly in IDistributedCache
        // This would require StackExchange.Redis for pattern matching
        // For now, we'll log a warning and skip pattern-based deletion
        _logger.LogWarning("Pattern-based cache removal not supported with IDistributedCache. Pattern: {Pattern}", pattern);
        await Task.CompletedTask;
    }
}

