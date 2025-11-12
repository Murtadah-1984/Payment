using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Implementation of payment processing service.
/// Single Responsibility: Only processes payments through providers.
/// Handles FX conversion when provider and payment currencies differ.
/// </summary>
public class PaymentProcessingService : IPaymentProcessingService
{
    private readonly ILogger<PaymentProcessingService> _logger;
    private readonly IFxConversionService? _fxConversionService;

    public PaymentProcessingService(
        ILogger<PaymentProcessingService> logger,
        IFxConversionService? fxConversionService = null)
    {
        _logger = logger;
        _fxConversionService = fxConversionService;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        PaymentEntity payment,
        IPaymentProvider provider,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating payment processing with provider {Provider} for payment {PaymentId}",
            provider.ProviderName, payment.Id.Value);

        var paymentCurrency = payment.Currency.Code;
        var paymentAmount = payment.Amount.Value;

        // Check if provider supports the payment currency
        var providerSupportsCurrency = PaymentProviderCatalog.ProviderSupportsCurrency(
            provider.ProviderName,
            paymentCurrency);

        // If provider doesn't support payment currency, convert to provider's primary currency
        if (!providerSupportsCurrency && _fxConversionService != null)
        {
            var providerPrimaryCurrency = PaymentProviderCatalog.GetProviderPrimaryCurrency(provider.ProviderName);
            
            if (string.IsNullOrWhiteSpace(providerPrimaryCurrency))
            {
                _logger.LogWarning(
                    "Provider {Provider} not found in catalog or has no supported currencies. Proceeding with original currency {Currency}",
                    provider.ProviderName, paymentCurrency);
            }
            else if (!string.Equals(paymentCurrency, providerPrimaryCurrency, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Payment currency {PaymentCurrency} not supported by provider {Provider}. Converting to {ProviderCurrency}",
                    paymentCurrency, provider.ProviderName, providerPrimaryCurrency);

                try
                {
                    var fxResult = await _fxConversionService.ConvertAsync(
                        paymentCurrency,
                        providerPrimaryCurrency,
                        paymentAmount,
                        cancellationToken);

                    paymentAmount = fxResult.ConvertedAmount;
                    paymentCurrency = providerPrimaryCurrency;

                    _logger.LogInformation(
                        "FX conversion completed: {OriginalAmount} {FromCurrency} → {ConvertedAmount} {ToCurrency} (Rate: {Rate})",
                        fxResult.OriginalAmount, fxResult.FromCurrency, fxResult.ConvertedAmount, fxResult.ToCurrency, fxResult.Rate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to convert currency from {FromCurrency} to {ToCurrency} for payment {PaymentId}. Proceeding with original currency.",
                        paymentCurrency, providerPrimaryCurrency, payment.Id.Value);
                    // Continue with original currency - provider may still accept it
                }
            }
        }

        // Prepare payment request for provider with potentially converted amount/currency
        var paymentRequest = new PaymentRequest(
            new Amount(paymentAmount),
            new Currency(paymentCurrency),
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

