using MediatR;
using Payment.Application.DTOs;
using Payment.Domain.Common;

namespace Payment.Application.Queries;

public sealed record GetPaymentByIdQuery(Guid PaymentId) : IRequest<Result<PaymentDto>>;

