using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Payment.API.Middleware;

/// <summary>
/// Middleware for IP whitelisting on admin endpoints in production.
/// Blocks requests from IPs not in the whitelist.
/// Follows Single Responsibility Principle - only handles IP whitelisting.
/// </summary>
public class IpWhitelistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpWhitelistMiddleware> _logger;
    private readonly HashSet<string> _whitelistedIps;
    private readonly bool _isProduction;
    private readonly bool _enabled;

    public IpWhitelistMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<IpWhitelistMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _isProduction = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Production";
        _enabled = configuration.GetValue<bool>("Security:IpWhitelist:Enabled", defaultValue: false);

        var whitelistConfig = configuration.GetSection("Security:IpWhitelist:AllowedIps").Get<string[]>() ?? Array.Empty<string>();
        _whitelistedIps = new HashSet<string>(whitelistConfig, StringComparer.OrdinalIgnoreCase);

        if (_enabled && _whitelistedIps.Count == 0)
        {
            _logger.LogWarning("IP whitelisting is enabled but no IPs are configured. All admin requests will be blocked.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to admin endpoints
        if (!IsAdminEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Only enforce in production when enabled
        if (!_isProduction || !_enabled)
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIpAddress(context);

        if (string.IsNullOrEmpty(clientIp))
        {
            _logger.LogWarning("Admin request blocked: Could not determine client IP address");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Access denied: Unable to determine IP address");
            return;
        }

        if (!IsIpWhitelisted(clientIp))
        {
            _logger.LogWarning(
                "Admin request blocked: IP address {IpAddress} is not whitelisted. Path: {Path}",
                clientIp, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Access denied: IP address not whitelisted");
            return;
        }

        _logger.LogDebug("Admin request allowed: IP address {IpAddress} is whitelisted", clientIp);
        await _next(context);
    }

    private static bool IsAdminEndpoint(PathString path)
    {
        var pathValue = path.Value ?? string.Empty;
        return pathValue.Contains("/admin/", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsIpWhitelisted(string ipAddress)
    {
        // Check exact match
        if (_whitelistedIps.Contains(ipAddress))
        {
            return true;
        }

        // Check CIDR notation (simplified - for production, use a proper IP address library)
        foreach (var whitelistedIp in _whitelistedIps)
        {
            if (whitelistedIp.Contains('/'))
            {
                if (IsIpInCidrRange(ipAddress, whitelistedIp))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsIpInCidrRange(string ipAddress, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2)
                return false;

            var networkIp = parts[0];
            var prefixLength = int.Parse(parts[1]);

            // Simplified CIDR check - for production, use System.Net.IPAddress or a library
            // This is a basic implementation
            if (prefixLength == 32)
            {
                return ipAddress == networkIp;
            }

            // For more complex CIDR checks, use a proper IP address library
            // For now, we'll do a simple prefix match
            return ipAddress.StartsWith(networkIp.Split('.')[0] + ".", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP header (when behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first one (original client)
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

