using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Commands;

/// <summary>
/// Command to complete 3D Secure authentication for a payment.
/// </summary>
public sealed record CompleteThreeDSecureCommand(
    Guid PaymentId,
    string Pareq,
    string Ares,
    string Md) : IRequest<ThreeDSecureResultDto>;

