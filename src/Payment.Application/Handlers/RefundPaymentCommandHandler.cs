using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Mappings;
using Payment.Domain.Common;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;

namespace Payment.Application.Handlers;

/// <summary>
/// Handler for refunding a payment.
/// Uses Result pattern for error handling (Result Pattern #16) and OpenTelemetry tracing (Observability #15).
/// Uses state machine for state transitions (State Machine #18).
/// </summary>
public sealed class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, Result<PaymentDto>>
{
    private static readonly ActivitySource ActivitySource = new("Payment.Application");
    
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentStateService _stateService;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;
    private readonly IMetricsRecorder _metricsRecorder;

    public RefundPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        IPaymentStateService stateService,
        ILogger<RefundPaymentCommandHandler> logger,
        IMetricsRecorder metricsRecorder)
    {
        _unitOfWork = unitOfWork;
        _stateService = stateService;
        _logger = logger;
        _metricsRecorder = metricsRecorder;
    }

    public async Task<Result<PaymentDto>> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("RefundPayment");
        activity?.SetTag("payment.id", request.PaymentId.ToString());
        activity?.SetTag("refund.transaction.id", request.RefundTransactionId);

        var refundStartTime = DateTime.UtcNow;
        try
        {
            var payment = await _unitOfWork.Payments.GetByIdAsync(request.PaymentId, cancellationToken);
            if (payment == null)
            {
                _logger.LogWarning("Payment {PaymentId} not found", request.PaymentId);
                activity?.SetTag("payment.found", false);
                activity?.SetStatus(ActivityStatusCode.Error, "Payment not found");
                return Result<PaymentDto>.Failure(ErrorCodes.PaymentNotFound, 
                    $"Payment with ID {request.PaymentId} not found");
            }

            activity?.SetTag("payment.found", true);
            activity?.SetTag("payment.status.before", payment.Status.ToString());

            // Validate payment status using state machine
            if (payment.Status == PaymentStatus.Refunded)
            {
                _logger.LogInformation("Payment {PaymentId} is already refunded", request.PaymentId);
                activity?.SetTag("payment.status", "already_refunded");
                return Result<PaymentDto>.Success(payment.ToDto());
            }

            // State machine will validate the transition
            payment.Refund(request.RefundTransactionId, _stateService);
            await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Record metrics for successful refund
            var refundDuration = (DateTime.UtcNow - refundStartTime).TotalSeconds;
            var provider = payment.Provider.ToString();
            _metricsRecorder.RecordRefund(provider, "succeeded", refundDuration);

            activity?.SetTag("payment.status.after", payment.Status.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Payment {PaymentId} refunded with transaction ID: {RefundTransactionId}", 
                request.PaymentId, request.RefundTransactionId);

            return Result<PaymentDto>.Success(payment.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding payment {PaymentId}", request.PaymentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            
            // Record metrics for failed refund
            var refundDuration = (DateTime.UtcNow - refundStartTime).TotalSeconds;
            _metricsRecorder.RecordRefund("unknown", "failed", refundDuration);
            _metricsRecorder.RecordError("refund_processing", "error", "RefundPaymentCommandHandler");
            
            return Result<PaymentDto>.Failure(ErrorCodes.InternalError, 
                $"An error occurred while refunding payment: {ex.Message}");
        }
    }
}

