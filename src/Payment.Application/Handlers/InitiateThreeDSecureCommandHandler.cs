using MediatR;
using Microsoft.Extensions.Logging;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Handlers;

/// <summary>
/// Handler for initiating 3D Secure authentication.
/// Follows Single Responsibility Principle - only handles 3DS initiation.
/// </summary>
public sealed class InitiateThreeDSecureCommandHandler : IRequestHandler<InitiateThreeDSecureCommand, ThreeDSecureChallengeDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IThreeDSecureService _threeDSecureService;
    private readonly ILogger<InitiateThreeDSecureCommandHandler> _logger;

    public InitiateThreeDSecureCommandHandler(
        IUnitOfWork unitOfWork,
        IThreeDSecureService threeDSecureService,
        ILogger<InitiateThreeDSecureCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _threeDSecureService = threeDSecureService ?? throw new ArgumentNullException(nameof(threeDSecureService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ThreeDSecureChallengeDto?> Handle(InitiateThreeDSecureCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initiating 3D Secure authentication for payment {PaymentId}", request.PaymentId);

        // Get payment
        var payment = await _unitOfWork.Payments.GetByIdAsync(
            new PaymentId(request.PaymentId), 
            cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found for 3DS initiation", request.PaymentId);
            throw new InvalidOperationException($"Payment {request.PaymentId} not found");
        }

        // Validate payment has card token
        if (payment.CardToken == null)
        {
            _logger.LogWarning("Payment {PaymentId} does not have a card token for 3DS", request.PaymentId);
            throw new InvalidOperationException("Payment must have a card token to initiate 3D Secure");
        }

        // Check if 3DS is required
        var isRequired = await _threeDSecureService.IsAuthenticationRequiredAsync(
            payment.Amount,
            payment.Currency,
            payment.CardToken,
            cancellationToken);

        if (!isRequired)
        {
            _logger.LogInformation("3D Secure not required for payment {PaymentId}", request.PaymentId);
            payment.SkipThreeDSecure();
            await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        // Initiate 3DS authentication
        var challenge = await _threeDSecureService.InitiateAuthenticationAsync(
            payment.Id.Value,
            payment.Amount,
            payment.Currency,
            payment.CardToken,
            request.ReturnUrl,
            cancellationToken);

        if (challenge == null)
        {
            _logger.LogWarning("3D Secure challenge not returned for payment {PaymentId}", request.PaymentId);
            payment.SkipThreeDSecure();
            await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        // Update payment with challenge
        payment.InitiateThreeDSecure(challenge);
        await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("3D Secure challenge initiated for payment {PaymentId}", request.PaymentId);

        return new ThreeDSecureChallengeDto(
            challenge.AcsUrl,
            challenge.Pareq,
            challenge.Md,
            challenge.TermUrl,
            challenge.Version);
    }
}

