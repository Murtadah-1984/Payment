using MediatR;
using Microsoft.Extensions.Logging;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Handlers;

/// <summary>
/// Handler for completing 3D Secure authentication.
/// Follows Single Responsibility Principle - only handles 3DS completion.
/// </summary>
public sealed class CompleteThreeDSecureCommandHandler : IRequestHandler<CompleteThreeDSecureCommand, ThreeDSecureResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IThreeDSecureService _threeDSecureService;
    private readonly ILogger<CompleteThreeDSecureCommandHandler> _logger;

    public CompleteThreeDSecureCommandHandler(
        IUnitOfWork unitOfWork,
        IThreeDSecureService threeDSecureService,
        ILogger<CompleteThreeDSecureCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _threeDSecureService = threeDSecureService ?? throw new ArgumentNullException(nameof(threeDSecureService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ThreeDSecureResultDto> Handle(CompleteThreeDSecureCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing 3D Secure authentication for payment {PaymentId}", request.PaymentId);

        // Get payment
        var payment = await _unitOfWork.Payments.GetByIdAsync(
            new PaymentId(request.PaymentId), 
            cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found for 3DS completion", request.PaymentId);
            throw new InvalidOperationException($"Payment {request.PaymentId} not found");
        }

        // Complete 3DS authentication
        var result = await _threeDSecureService.CompleteAuthenticationAsync(
            payment.Id.Value,
            request.Pareq,
            request.Ares,
            request.Md,
            cancellationToken);

        // Update payment with result
        payment.CompleteThreeDSecure(result);
        await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "3D Secure authentication completed for payment {PaymentId}. Authenticated: {Authenticated}",
            request.PaymentId, result.Authenticated);

        return new ThreeDSecureResultDto(
            result.Authenticated,
            result.Cavv,
            result.Eci,
            result.Xid,
            result.Version,
            result.FailureReason);
    }
}

