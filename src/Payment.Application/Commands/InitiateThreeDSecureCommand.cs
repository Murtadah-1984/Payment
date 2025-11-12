using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Commands;

/// <summary>
/// Command to initiate 3D Secure authentication for a payment.
/// </summary>
public sealed record InitiateThreeDSecureCommand(
    Guid PaymentId,
    string ReturnUrl) : IRequest<ThreeDSecureChallengeDto?>;

