using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Payment.Application.DTOs;

namespace Payment.Application.Services;

/// <summary>
/// Implementation of request hash service using SHA-256.
/// Computes canonical JSON hash to ensure idempotency key validation.
/// </summary>
public class RequestHashService : IRequestHashService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public string ComputeRequestHash(CreatePaymentDto request)
    {
        // Create a canonical representation of the request
        // Sort metadata keys to ensure consistent hashing
        var canonicalRequest = new
        {
            request.RequestId,
            request.Amount,
            request.Currency,
            request.PaymentMethod,
            request.Provider,
            request.MerchantId,
            request.OrderId,
            request.ProjectCode,
            request.SystemFeePercent,
            request.SplitRule,
            Metadata = request.Metadata?.OrderBy(kvp => kvp.Key)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            request.CallbackUrl,
            request.CustomerEmail,
            request.CustomerPhone
        };

        // Serialize to JSON
        var json = JsonSerializer.Serialize(canonicalRequest, JsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        // Compute SHA-256 hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(jsonBytes);

        // Convert to hexadecimal string
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}


