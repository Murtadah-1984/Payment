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
/// Handler for failing a payment.
/// Uses Result pattern for error handling (Result Pattern #16) and OpenTelemetry tracing (Observability #15).
/// Uses state machine for state transitions (State Machine #18).
/// </summary>
public sealed class FailPaymentCommandHandler : IRequestHandler<FailPaymentCommand, Result<PaymentDto>>
{
    private static readonly ActivitySource ActivitySource = new("Payment.Application");
    
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentStateService _stateService;
    private readonly ILogger<FailPaymentCommandHandler> _logger;

    public FailPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        IPaymentStateService stateService,
        ILogger<FailPaymentCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _stateService = stateService;
        _logger = logger;
    }

    public async Task<Result<PaymentDto>> Handle(FailPaymentCommand request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("FailPayment");
        activity?.SetTag("payment.id", request.PaymentId.ToString());
        activity?.SetTag("payment.failure.reason", request.Reason);

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
            if (payment.Status == PaymentStatus.Failed)
            {
                _logger.LogInformation("Payment {PaymentId} is already failed", request.PaymentId);
                activity?.SetTag("payment.status", "already_failed");
                return Result<PaymentDto>.Success(payment.ToDto());
            }

            // State machine will validate the transition
            payment.Fail(request.Reason, _stateService);
            await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            activity?.SetTag("payment.status.after", payment.Status.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Payment {PaymentId} failed with reason: {Reason}", request.PaymentId, request.Reason);

            return Result<PaymentDto>.Success(payment.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error failing payment {PaymentId}", request.PaymentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            return Result<PaymentDto>.Failure(ErrorCodes.InternalError, 
                $"An error occurred while failing payment: {ex.Message}");
        }
    }
}

