using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Application.Services;

/// <summary>
/// Service for handling multi-currency settlement.
/// Implements automatic currency conversion when payment currency differs from settlement currency.
/// Follows SOLID principles: Single Responsibility (settlement only), Dependency Inversion (depends on abstractions).
/// </summary>
public class SettlementService : ISettlementService
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ILogger<SettlementService> _logger;
    private readonly string _defaultSettlementCurrency;

    public SettlementService(
        IExchangeRateService exchangeRateService,
        ILogger<SettlementService> logger,
        IConfiguration configuration)
    {
        _exchangeRateService = exchangeRateService ?? throw new ArgumentNullException(nameof(exchangeRateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultSettlementCurrency = configuration["Settlement:Currency"] ?? "USD";
    }

    public async Task<bool> ProcessSettlementAsync(
        Payment payment,
        string settlementCurrency,
        CancellationToken cancellationToken = default)
    {
        if (payment == null)
            throw new ArgumentNullException(nameof(payment));

        if (string.IsNullOrWhiteSpace(settlementCurrency))
            settlementCurrency = _defaultSettlementCurrency;

        var settlementCurrencyUpper = settlementCurrency.ToUpperInvariant();
        var paymentCurrencyUpper = payment.Currency.Code.ToUpperInvariant();

        // No conversion needed if currencies match
        if (paymentCurrencyUpper == settlementCurrencyUpper)
        {
            _logger.LogDebug(
                "Payment {PaymentId} settlement currency matches payment currency {Currency}. No conversion needed.",
                payment.Id.Value, paymentCurrencyUpper);
            return false;
        }

        try
        {
            // Get exchange rate at payment completion time
            var exchangeRate = await _exchangeRateService.GetExchangeRateAsync(
                paymentCurrencyUpper,
                settlementCurrencyUpper,
                payment.UpdatedAt,
                cancellationToken);

            // Convert payment amount to settlement currency
            var settlementAmount = await _exchangeRateService.ConvertAsync(
                payment.Amount.Value,
                paymentCurrencyUpper,
                settlementCurrencyUpper,
                payment.UpdatedAt,
                cancellationToken);

            // Set settlement information on payment entity
            var settlementCurrencyVo = Currency.FromCode(settlementCurrencyUpper);
            payment.SetSettlement(settlementCurrencyVo, settlementAmount, exchangeRate);

            _logger.LogInformation(
                "Payment {PaymentId} settled: {PaymentAmount} {PaymentCurrency} = {SettlementAmount} {SettlementCurrency} (Rate: {ExchangeRate})",
                payment.Id.Value,
                payment.Amount.Value,
                paymentCurrencyUpper,
                settlementAmount,
                settlementCurrencyUpper,
                exchangeRate);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process settlement for payment {PaymentId} from {FromCurrency} to {ToCurrency}",
                payment.Id.Value, paymentCurrencyUpper, settlementCurrencyUpper);
            
            // Don't throw - allow payment to complete without settlement conversion
            // This ensures payment completion is not blocked by settlement issues
            return false;
        }
    }
}

