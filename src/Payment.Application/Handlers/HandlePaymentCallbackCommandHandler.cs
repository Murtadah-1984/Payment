using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Mappings;
using Payment.Application.Services;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Handlers;

/// <summary>
/// Handler for processing payment provider callbacks.
/// Follows Single Responsibility Principle - only handles callback processing.
/// Uses state machine for state transitions (State Machine #18).
/// </summary>
public sealed class HandlePaymentCallbackCommandHandler : IRequestHandler<HandlePaymentCallbackCommand, PaymentDto?>
{
    private readonly IPaymentProviderFactory _providerFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentStateService _stateService;
    private readonly IPaymentWebhookNotifier? _webhookNotifier;
    private readonly ISettlementService? _settlementService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HandlePaymentCallbackCommandHandler> _logger;

    public HandlePaymentCallbackCommandHandler(
        IPaymentProviderFactory providerFactory,
        IUnitOfWork unitOfWork,
        IPaymentStateService stateService,
        IConfiguration configuration,
        ILogger<HandlePaymentCallbackCommandHandler> logger,
        IPaymentWebhookNotifier? webhookNotifier = null,
        ISettlementService? settlementService = null)
    {
        _providerFactory = providerFactory;
        _unitOfWork = unitOfWork;
        _stateService = stateService;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _webhookNotifier = webhookNotifier;
        _settlementService = settlementService;
    }

    public async Task<PaymentDto?> Handle(HandlePaymentCallbackCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing callback for provider {Provider}", request.Provider);

        try
        {
            // Get the provider (Feature Flags #17 - supports feature flag checks)
            var provider = await _providerFactory.CreateAsync(request.Provider, cancellationToken);

            // Check if provider supports callbacks
            if (provider is not IPaymentCallbackProvider callbackProvider)
            {
                _logger.LogWarning("Provider {Provider} does not support callback verification", request.Provider);
                return null;
            }

            // Verify the callback
            var result = await callbackProvider.VerifyCallbackAsync(request.CallbackData, cancellationToken);

            if (!result.Success || string.IsNullOrEmpty(result.TransactionId))
            {
                _logger.LogWarning("Callback verification failed for provider {Provider}: {Reason}",
                    request.Provider, result.FailureReason);
                return null;
            }

            // Find payment by transaction ID or order ID
            var transactionId = result.TransactionId;
            var orderId = request.CallbackData.GetValueOrDefault("orderId") 
                       ?? request.CallbackData.GetValueOrDefault("order_id")
                       ?? result.ProviderMetadata?.GetValueOrDefault("OrderId");

            PaymentEntity? payment = null;

            if (!string.IsNullOrEmpty(transactionId))
            {
                payment = await _unitOfWork.Payments.GetByTransactionIdAsync(transactionId, cancellationToken);
            }

            if (payment == null && !string.IsNullOrEmpty(orderId))
            {
                payment = await _unitOfWork.Payments.GetByOrderIdAsync(orderId, cancellationToken);
            }

            if (payment == null)
            {
                _logger.LogWarning("Payment not found for transaction {TransactionId} or order {OrderId}",
                    transactionId, orderId);
                return null;
            }

            // Update payment status based on callback result using state machine
            if (result.Success)
            {
                if (payment.Status == Domain.Enums.PaymentStatus.Pending)
                {
                    payment.Process(transactionId, _stateService);
                }

                if (payment.Status == Domain.Enums.PaymentStatus.Processing)
                {
                    payment.Complete(_stateService);
                    
                    // Process multi-currency settlement if enabled (Multi-Currency Settlement #21)
                    if (_settlementService != null)
                    {
                        var settlementCurrency = _configuration["Settlement:Currency"] ?? "USD";
                        await _settlementService.ProcessSettlementAsync(payment, settlementCurrency, cancellationToken);
                    }
                }
            }
            else
            {
                payment.Fail(result.FailureReason ?? "Callback verification failed", _stateService);
            }

            // Update metadata if provided
            if (result.ProviderMetadata != null)
            {
                foreach (var kvp in result.ProviderMetadata)
                {
                    payment.Metadata[kvp.Key] = kvp.Value;
                }
            }

            await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Payment {PaymentId} updated successfully from callback", payment.Id.Value);

            // Notify webhook subscribers about payment status change
            if (_webhookNotifier != null)
            {
                var eventType = payment.Status switch
                {
                    Domain.Enums.PaymentStatus.Processing => "payment.processing",
                    Domain.Enums.PaymentStatus.Succeeded => "payment.completed",
                    Domain.Enums.PaymentStatus.Failed => "payment.failed",
                    Domain.Enums.PaymentStatus.Refunded => "payment.refunded",
                    _ => "payment.updated"
                };

                await _webhookNotifier.NotifyPaymentStatusChangeAsync(payment, eventType, cancellationToken);
            }

            return payment.ToDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing callback for provider {Provider}", request.Provider);
            throw;
        }
    }
}

