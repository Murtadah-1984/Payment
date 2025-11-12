using Payment.Domain.Interfaces;

namespace Payment.API.Middleware;

/// <summary>
/// Middleware for tracking rate limit hits.
/// Records metrics when rate limiting is triggered (429 Too Many Requests).
/// </summary>
public class RateLimitMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMetricsMiddleware> _logger;

    public RateLimitMetricsMiddleware(
        RequestDelegate next,
        ILogger<RateLimitMetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        await _next(context);

        // If rate limit was hit (429 Too Many Requests), record metrics
        if (context.Response.StatusCode == 429)
        {
            var metricsRecorder = serviceProvider.GetService<IMetricsRecorder>();
            if (metricsRecorder != null)
            {
                var endpoint = context.Request.Path.Value ?? "unknown";
                var ipAddress = GetClientIpAddress(context);
                
                metricsRecorder.RecordRateLimitHit(endpoint, ipAddress);
                
                _logger.LogDebug("Recorded rate limit hit: {Endpoint}, IP: {IpAddress}", endpoint, ipAddress);
            }
        }
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP header (when behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first one
            return forwardedFor.Split(',')[0].Trim();
        }

        // Check for real IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
}

