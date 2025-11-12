using MediatR;
using Payment.Application.DTOs;
using Payment.Domain.Common;

namespace Payment.Application.Commands;

public sealed record FailPaymentCommand(
    Guid PaymentId,
    string Reason) : IRequest<Result<PaymentDto>>;

