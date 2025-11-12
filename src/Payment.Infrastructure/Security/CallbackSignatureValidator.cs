using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Payment.Infrastructure.Security;

/// <summary>
/// Default implementation of webhook signature validation.
/// Uses HMAC-SHA256 for signature computation and validation.
/// </summary>
public class CallbackSignatureValidator : ICallbackSignatureValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CallbackSignatureValidator> _logger;

    public CallbackSignatureValidator(
        IConfiguration configuration,
        ILogger<CallbackSignatureValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<bool> ValidateAsync(string provider, string payload, string? signature, string? timestamp)
    {
        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Missing signature for provider {Provider}", provider);
            return Task.FromResult(false);
        }

        // Get webhook secret from configuration
        var webhookSecret = _configuration[$"PaymentProviders:{provider}:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogWarning("Webhook secret not configured for provider {Provider}", provider);
            return Task.FromResult(false);
        }

        // Compute expected signature
        string computedSignature;
        
        // Different providers may use different signature computation methods
        // Default: HMAC-SHA256 of (payload + timestamp) or just payload
        if (!string.IsNullOrEmpty(timestamp))
        {
            computedSignature = ComputeHmacSha256(payload + timestamp, webhookSecret);
        }
        else
        {
            computedSignature = ComputeHmacSha256(payload, webhookSecret);
        }

        // Constant-time comparison to prevent timing attacks
        var isValid = ConstantTimeEquals(signature, computedSignature);

        if (!isValid)
        {
            _logger.LogWarning(
                "Signature validation failed for provider {Provider}. Expected: {Expected}, Received: {Received}",
                provider, computedSignature, signature);
        }

        return Task.FromResult(isValid);
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks.
    /// </summary>
    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a == null || b == null)
            return a == b;

        if (a.Length != b.Length)
            return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}

