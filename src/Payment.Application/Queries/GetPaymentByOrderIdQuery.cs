using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Queries;

public sealed record GetPaymentByOrderIdQuery(string OrderId) : IRequest<PaymentDto?>;

