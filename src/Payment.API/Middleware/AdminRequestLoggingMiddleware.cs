using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Payment.API.Middleware;

/// <summary>
/// Middleware for logging all requests and responses to admin endpoints.
/// Provides audit trail for all administrative actions.
/// Follows Single Responsibility Principle - only handles request/response logging.
/// </summary>
public class AdminRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminRequestLoggingMiddleware> _logger;

    public AdminRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<AdminRequestLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only log admin endpoints
        if (!IsAdminEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();
        
        // Log request
        await LogRequestAsync(context, requestId);

        // Capture original response body stream
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);

            // Copy response back to original stream
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private static bool IsAdminEndpoint(PathString path)
    {
        var pathValue = path.Value ?? string.Empty;
        return pathValue.Contains("/admin/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task LogRequestAsync(HttpContext context, string requestId)
    {
        var request = context.Request;
        var userId = context.User?.FindFirst("sub")?.Value 
                    ?? context.User?.FindFirst("nameid")?.Value 
                    ?? "Anonymous";
        var ipAddress = GetClientIpAddress(context);
        var userAgent = request.Headers["User-Agent"].ToString();

        var logMessage = new StringBuilder();
        logMessage.AppendLine($"Admin Request [{requestId}]");
        logMessage.AppendLine($"  Method: {request.Method}");
        logMessage.AppendLine($"  Path: {request.Path}");
        logMessage.AppendLine($"  QueryString: {request.QueryString}");
        logMessage.AppendLine($"  UserId: {userId}");
        logMessage.AppendLine($"  IpAddress: {ipAddress}");
        logMessage.AppendLine($"  UserAgent: {userAgent}");

        // Log request body for POST/PUT/PATCH
        if (request.Method is "POST" or "PUT" or "PATCH")
        {
            request.EnableBuffering();
            var body = await ReadRequestBodyAsync(request);
            if (!string.IsNullOrEmpty(body))
            {
                logMessage.AppendLine($"  RequestBody: {body}");
            }
        }

        _logger.LogInformation("Admin Request: {RequestDetails}", logMessage.ToString());
    }

    private async Task LogResponseAsync(HttpContext context, string requestId, long elapsedMilliseconds)
    {
        var response = context.Response;
        var userId = context.User?.FindFirst("sub")?.Value 
                    ?? context.User?.FindFirst("nameid")?.Value 
                    ?? "Anonymous";

        var logMessage = new StringBuilder();
        logMessage.AppendLine($"Admin Response [{requestId}]");
        logMessage.AppendLine($"  StatusCode: {response.StatusCode}");
        logMessage.AppendLine($"  ElapsedMs: {elapsedMilliseconds}");
        logMessage.AppendLine($"  UserId: {userId}");

        // Log response body for non-successful responses or if it's small
        if (response.StatusCode >= 400 || response.ContentLength < 10240) // 10KB
        {
            var body = await ReadResponseBodyAsync(response);
            if (!string.IsNullOrEmpty(body))
            {
                logMessage.AppendLine($"  ResponseBody: {body}");
            }
        }

        var logLevel = response.StatusCode >= 500 ? LogLevel.Error
                     : response.StatusCode >= 400 ? LogLevel.Warning
                     : LogLevel.Information;

        _logger.Log(logLevel, "Admin Response: {ResponseDetails}", logMessage.ToString());
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        try
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        try
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return body;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP header (when behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
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

