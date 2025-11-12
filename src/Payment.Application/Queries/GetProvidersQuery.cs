using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Queries;

/// <summary>
/// CQRS Query to get payment providers with optional filtering by country, currency, or payment method.
/// Follows Clean Architecture - Application layer query.
/// </summary>
public sealed record GetProvidersQuery(
    string? Country,
    string? Currency,
    string? Method) : IRequest<IEnumerable<PaymentProviderInfoDto>>;

