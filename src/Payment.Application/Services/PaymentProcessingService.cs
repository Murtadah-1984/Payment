using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Implementation of payment processing service.
/// Single Responsibility: Only processes payments through providers.
/// </summary>
public class PaymentProcessingService : IPaymentProcessingService
{
    private readonly ILogger<PaymentProcessingService> _logger;

    public PaymentProcessingService(ILogger<PaymentProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        PaymentEntity payment,
        IPaymentProvider provider,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating payment processing with provider {Provider} for payment {PaymentId}",
            provider.ProviderName, payment.Id.Value);

        // Prepare payment request for provider
        var paymentRequest = new PaymentRequest(
            payment.Amount,
            payment.Currency,
            payment.MerchantId,
            payment.OrderId,
            payment.SplitPayment,
            payment.Metadata);

        // Process payment through provider
        var result = await provider.ProcessPaymentAsync(paymentRequest, cancellationToken);

        _logger.LogInformation("Payment {PaymentId} processed. Success: {Success}, TransactionId: {TransactionId}",
            payment.Id.Value, result.Success, result.TransactionId);

        return result;
    }
}

