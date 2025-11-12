using Payment.Domain.ValueObjects;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Core interface for payment providers.
/// Follows Interface Segregation Principle - basic payment processing only.
/// For 3DS support, providers should also implement IThreeDSecurePaymentProvider.
/// </summary>
public interface IPaymentProvider
{
    string ProviderName { get; }
    
    /// <summary>
    /// Processes a payment through the payment provider.
    /// </summary>
    /// <param name="request">The payment request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The payment result</returns>
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if this provider supports 3D Secure authentication.
    /// </summary>
    /// <returns>True if the provider supports 3DS, false otherwise</returns>
    bool SupportsThreeDSecure() => this is IThreeDSecurePaymentProvider;
}

public sealed record PaymentRequest(
    Amount Amount,
    Currency Currency,
    string MerchantId,
    string OrderId,
    SplitPayment? SplitPayment,
    Dictionary<string, string>? Metadata);

public sealed record PaymentResult(
    bool Success,
    string? TransactionId,
    string? FailureReason,
    Dictionary<string, string>? ProviderMetadata);

