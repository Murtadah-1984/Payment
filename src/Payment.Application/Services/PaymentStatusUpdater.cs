using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Implementation of payment status updater.
/// Single Responsibility: Only updates payment status based on results.
/// Uses state machine for state transitions (State Machine #18).
/// </summary>
public class PaymentStatusUpdater : IPaymentStatusUpdater
{
    private readonly IPaymentStateService _stateService;
    private readonly ILogger<PaymentStatusUpdater> _logger;

    public PaymentStatusUpdater(
        IPaymentStateService stateService,
        ILogger<PaymentStatusUpdater> logger)
    {
        _stateService = stateService;
        _logger = logger;
    }

    public void UpdatePaymentStatus(PaymentEntity payment, PaymentResult result)
    {
        if (result.Success)
        {
            var transactionId = result.TransactionId ?? payment.Id.Value.ToString();
            payment.Process(transactionId, _stateService);
            payment.Complete(_stateService);
            _logger.LogInformation("Payment {PaymentId} completed successfully with transaction {TransactionId}",
                payment.Id.Value, transactionId);
        }
        else
        {
            payment.Fail(result.FailureReason ?? "Payment processing failed", _stateService);
            _logger.LogWarning("Payment {PaymentId} failed: {Reason}",
                payment.Id.Value, result.FailureReason);
        }
    }
}

