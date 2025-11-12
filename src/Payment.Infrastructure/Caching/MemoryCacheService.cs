using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using System.Text.Json;

namespace Payment.Infrastructure.Caching;

/// <summary>
/// In-memory cache service implementation for development/testing (Caching Strategy #9).
/// Falls back to memory cache when Redis is not available.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly IMetricsRecorder? _metricsRecorder;

    public MemoryCacheService(
        IMemoryCache cache, 
        ILogger<MemoryCacheService> logger,
        IMetricsRecorder? metricsRecorder = null)
    {
        _cache = cache;
        _logger = logger;
        _metricsRecorder = metricsRecorder;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                _metricsRecorder?.RecordCacheOperation("get", "hit");
                return Task.FromResult(cached as T);
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            _metricsRecorder?.RecordCacheOperation("get", "miss");
            return Task.FromResult<T?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10)
            };

            _cache.Set(key, value, options);
            _logger.LogDebug("Cached value for key: {Key} with expiry: {Expiry}", key, expiry);
            _metricsRecorder?.RecordCacheOperation("set", "success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _cache.Remove(key);
            _logger.LogDebug("Removed cache key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Memory cache doesn't support pattern-based deletion
        _logger.LogWarning("Pattern-based cache removal not supported with IMemoryCache. Pattern: {Pattern}", pattern);
        return Task.CompletedTask;
    }
}

