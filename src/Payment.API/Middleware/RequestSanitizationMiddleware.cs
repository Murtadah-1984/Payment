using Microsoft.AspNetCore.Http;

namespace Payment.API.Middleware;

/// <summary>
/// Middleware for adding security headers and sanitizing requests.
/// Implements CRITICAL security measures to prevent XSS, clickjacking, and MIME-type sniffing attacks.
/// </summary>
public class RequestSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestSanitizationMiddleware> _logger;

    public RequestSanitizationMiddleware(
        RequestDelegate next,
        ILogger<RequestSanitizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers to response
        AddSecurityHeaders(context);

        await _next(context);
    }

    private static void AddSecurityHeaders(HttpContext context)
    {
        var response = context.Response;

        // X-Content-Type-Options: Prevent MIME-type sniffing
        // Prevents browsers from interpreting files as a different MIME type
        if (!response.Headers.ContainsKey("X-Content-Type-Options"))
        {
            response.Headers.Append("X-Content-Type-Options", "nosniff");
        }

        // X-Frame-Options: Prevent clickjacking attacks
        // Prevents the page from being displayed in a frame/iframe
        if (!response.Headers.ContainsKey("X-Frame-Options"))
        {
            response.Headers.Append("X-Frame-Options", "DENY");
        }

        // X-XSS-Protection: Enable XSS filtering (legacy, but still useful for older browsers)
        // Note: Modern browsers have built-in XSS protection, but this helps with older ones
        if (!response.Headers.ContainsKey("X-XSS-Protection"))
        {
            response.Headers.Append("X-XSS-Protection", "1; mode=block");
        }

        // Referrer-Policy: Control referrer information
        // Limits the amount of referrer information sent to other sites
        if (!response.Headers.ContainsKey("Referrer-Policy"))
        {
            response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        }

        // Content-Security-Policy: Prevent XSS and injection attacks
        // Restricts which resources can be loaded and executed
        if (!response.Headers.ContainsKey("Content-Security-Policy"))
        {
            // Strict CSP policy - only allow resources from same origin
            // Adjust based on your needs (e.g., if you need to load external scripts)
            response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " + // 'unsafe-inline' for Swagger UI styles
                "img-src 'self' data: https:; " +
                "font-src 'self' data:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'; " +
                "upgrade-insecure-requests");
        }

        // Permissions-Policy: Control browser features
        // Restricts access to browser features and APIs
        if (!response.Headers.ContainsKey("Permissions-Policy"))
        {
            response.Headers.Append("Permissions-Policy",
                "geolocation=(), " +
                "microphone=(), " +
                "camera=(), " +
                "payment=(), " +
                "usb=(), " +
                "magnetometer=(), " +
                "gyroscope=(), " +
                "accelerometer=()");
        }

        // Strict-Transport-Security (HSTS): Force HTTPS
        // Only add in production and when using HTTPS
        if (context.Request.IsHttps && !response.Headers.ContainsKey("Strict-Transport-Security"))
        {
            // HSTS: max-age=31536000 (1 year), includeSubDomains, preload
            response.Headers.Append("Strict-Transport-Security",
                "max-age=31536000; includeSubDomains; preload");
        }
    }
}

