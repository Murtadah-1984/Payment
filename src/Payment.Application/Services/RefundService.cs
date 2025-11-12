using MediatR;
using Microsoft.Extensions.Logging;
using Payment.Application.Commands;
using Payment.Domain.Common;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Application.Services;

/// <summary>
/// Service for processing refunds.
/// Orchestrates refund operations using MediatR commands.
/// Follows Single Responsibility Principle - only handles refund orchestration.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class RefundService : IRefundService
{
    private readonly IMediator _mediator;
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        IMediator mediator,
        ILogger<RefundService> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ProcessRefundAsync(
        PaymentId paymentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (paymentId == null)
            throw new ArgumentNullException(nameof(paymentId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be null or empty", nameof(reason));

        _logger.LogInformation(
            "Processing refund for PaymentId: {PaymentId}, Reason: {Reason}",
            paymentId.Value, reason);

        try
        {
            var command = new RefundPaymentCommand(
                PaymentId: paymentId.Value,
                RefundTransactionId: $"REF-{Guid.NewGuid()}");

            _logger.LogDebug("Refund reason: {Reason}", reason);

            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Refund processed successfully for PaymentId: {PaymentId}", paymentId.Value);
                return true;
            }
            else
            {
                _logger.LogWarning(
                    "Refund failed for PaymentId: {PaymentId}, Error: {Error}",
                    paymentId.Value, result.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for PaymentId: {PaymentId}", paymentId.Value);
            return false;
        }
    }

    public async Task<Dictionary<PaymentId, bool>> ProcessRefundsAsync(
        IEnumerable<PaymentId> paymentIds,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (paymentIds == null)
            throw new ArgumentNullException(nameof(paymentIds));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be null or empty", nameof(reason));

        var paymentIdList = paymentIds.ToList();
        var results = new Dictionary<PaymentId, bool>();

        _logger.LogInformation(
            "Processing batch refunds for {Count} payments, Reason: {Reason}",
            paymentIdList.Count, reason);

        foreach (var paymentId in paymentIdList)
        {
            var success = await ProcessRefundAsync(paymentId, reason, cancellationToken);
            results[paymentId] = success;
        }

        var successCount = results.Values.Count(r => r);
        _logger.LogInformation(
            "Batch refunds completed. Success: {SuccessCount}/{TotalCount}",
            successCount, paymentIdList.Count);

        return results;
    }
}

