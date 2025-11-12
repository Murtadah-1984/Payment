using Payment.Domain.Interfaces;

namespace Payment.API.Middleware;

/// <summary>
/// Middleware for tracking authentication failures.
/// Records metrics when authentication fails.
/// </summary>
public class AuthenticationMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMetricsMiddleware> _logger;

    public AuthenticationMetricsMiddleware(
        RequestDelegate next,
        ILogger<AuthenticationMetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        // Check authentication status after the authentication middleware runs
        await _next(context);

        // If authentication failed (401 Unauthorized), record metrics
        if (context.Response.StatusCode == 401)
        {
            var metricsRecorder = serviceProvider.GetService<IMetricsRecorder>();
            if (metricsRecorder != null)
            {
                // Determine failure reason from context
                var failureReason = DetermineFailureReason(context);
                metricsRecorder.RecordAuthenticationFailure(failureReason);
                
                _logger.LogDebug("Recorded authentication failure: {Reason}", failureReason);
            }
        }
    }

    private static string DetermineFailureReason(HttpContext context)
    {
        // Check if token is missing
        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return "missing_token";
        }

        // Check for expired token (common JWT error)
        var error = context.Items["AuthenticationError"]?.ToString();
        if (error != null)
        {
            if (error.Contains("expired", StringComparison.OrdinalIgnoreCase))
            {
                return "expired_token";
            }
            if (error.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return "invalid_token";
            }
        }

        // Default to invalid_token if we can't determine the specific reason
        return "invalid_token";
    }
}

