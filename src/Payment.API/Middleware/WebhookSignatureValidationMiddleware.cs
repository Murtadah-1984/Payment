using Microsoft.Extensions.Primitives;
using Payment.Domain.Interfaces;
using System.Text;

namespace Payment.API.Middleware;

/// <summary>
/// Middleware for validating webhook signatures from payment providers.
/// Prevents unauthorized callbacks by validating signatures before processing.
/// </summary>
public class WebhookSignatureValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebhookSignatureValidationMiddleware> _logger;
    private readonly IServiceProvider _serviceProvider;
    private const int MaxTimestampAgeMinutes = 5;

    public WebhookSignatureValidationMiddleware(
        RequestDelegate next,
        ILogger<WebhookSignatureValidationMiddleware> logger,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if this is a callback endpoint
        if (IsCallbackEndpoint(context.Request.Path))
        {
            _logger.LogInformation("Processing webhook signature validation for path: {Path}", context.Request.Path);

            // Extract provider from route
            var provider = ExtractProvider(context.Request.Path);
            if (string.IsNullOrEmpty(provider))
            {
                _logger.LogWarning("Could not extract provider from path: {Path}", context.Request.Path);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid callback endpoint");
                return;
            }

            // Enable buffering to read body multiple times
            context.Request.EnableBuffering();

            // Read body
            string body;
            using (var reader = new StreamReader(
                context.Request.Body,
                encoding: Encoding.UTF8,
                leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0; // Reset stream position
            }

            // Get signature and timestamp from headers
            // Different providers may use different header names, so we check multiple
            var signature = GetSignatureHeader(context.Request.Headers, provider);
            var timestamp = GetTimestampHeader(context.Request.Headers, provider);

            // Validate timestamp (prevent replay attacks)
            if (!string.IsNullOrEmpty(timestamp) && !IsTimestampValid(timestamp))
            {
                _logger.LogWarning(
                    "Invalid timestamp for provider {Provider}. Timestamp: {Timestamp}, Path: {Path}",
                    provider, timestamp, context.Request.Path);
                
                // Record metrics for webhook signature failure
                var metricsRecorder = _serviceProvider.GetService<IMetricsRecorder>();
                metricsRecorder?.RecordWebhookSignatureFailure(provider, "expired_timestamp");
                
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid or expired timestamp");
                return;
            }

            // Validate signature using provider-specific validation
            var validator = _serviceProvider.GetRequiredService<ICallbackSignatureValidator>();
            var isValid = await validator.ValidateAsync(provider, body, signature, timestamp);

            if (!isValid)
            {
                _logger.LogWarning(
                    "Invalid webhook signature for provider {Provider}. Path: {Path}",
                    provider, context.Request.Path);
                
                // Record metrics for webhook signature failure
                var metricsRecorder = _serviceProvider.GetService<IMetricsRecorder>();
                var failureReason = string.IsNullOrEmpty(signature) ? "missing_signature" : "invalid_signature";
                metricsRecorder?.RecordWebhookSignatureFailure(provider, failureReason);
                
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid webhook signature");
                return;
            }

            _logger.LogInformation("Webhook signature validated successfully for provider {Provider}", provider);
        }

        await _next(context);
    }

    private static bool IsCallbackEndpoint(PathString path)
    {
        return path.StartsWithSegments("/api/v1/payments") &&
               (path.Value?.Contains("/callback", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string? ExtractProvider(PathString path)
    {
        if (string.IsNullOrEmpty(path.Value))
            return null;

        var segments = path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Expected format: /api/v1/payments/{provider}/callback
        // Or: /api/v1/payments/{provider}/callback?params
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Equals("payments", StringComparison.OrdinalIgnoreCase) && 
                i + 1 < segments.Length)
            {
                var providerSegment = segments[i + 1];
                // Remove query string if present
                var provider = providerSegment.Split('?')[0];
                return provider;
            }
        }

        return null;
    }

    private static string? GetSignatureHeader(IHeaderDictionary headers, string provider)
    {
        // Check common signature header names
        var headerNames = new[]
        {
            "X-Signature",
            "X-Webhook-Signature",
            "Signature",
            $"X-{provider}-Signature",
            "X-Hub-Signature-256", // GitHub-style
            "X-Stripe-Signature" // Stripe-style
        };

        foreach (var headerName in headerNames)
        {
            if (headers.TryGetValue(headerName, out StringValues values) && 
                !string.IsNullOrEmpty(values.ToString()))
            {
                return values.ToString();
            }
        }

        return null;
    }

    private static string? GetTimestampHeader(IHeaderDictionary headers, string provider)
    {
        // Check common timestamp header names
        var headerNames = new[]
        {
            "X-Timestamp",
            "X-Webhook-Timestamp",
            "Timestamp",
            $"X-{provider}-Timestamp",
            "X-Request-Timestamp"
        };

        foreach (var headerName in headerNames)
        {
            if (headers.TryGetValue(headerName, out StringValues values) && 
                !string.IsNullOrEmpty(values.ToString()))
            {
                return values.ToString();
            }
        }

        return null;
    }

    private static bool IsTimestampValid(string? timestamp)
    {
        if (string.IsNullOrEmpty(timestamp))
            return true; // If no timestamp, we'll still validate signature

        // Try parsing as Unix timestamp (seconds or milliseconds)
        if (long.TryParse(timestamp, out long unixTimestamp))
        {
            // Handle both seconds and milliseconds
            if (unixTimestamp > 1_000_000_000_000) // Milliseconds
            {
                unixTimestamp /= 1000;
            }

            var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
            var now = DateTime.UtcNow;
            var age = now - requestTime;

            // Reject requests older than MaxTimestampAgeMinutes
            if (Math.Abs(age.TotalMinutes) > MaxTimestampAgeMinutes)
            {
                return false;
            }

            return true;
        }

        // Try parsing as ISO 8601 format
        if (DateTimeOffset.TryParse(timestamp, out DateTimeOffset parsedTimestamp))
        {
            var now = DateTimeOffset.UtcNow;
            var age = now - parsedTimestamp;

            if (Math.Abs(age.TotalMinutes) > MaxTimestampAgeMinutes)
            {
                return false;
            }

            return true;
        }

        // If we can't parse it, reject it (fail secure)
        return false;
    }
}

