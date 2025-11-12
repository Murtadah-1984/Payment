using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Queries;

/// <summary>
/// CQRS Query to get payment providers by country code.
/// Follows Clean Architecture - Application layer query.
/// </summary>
public sealed record GetPaymentProvidersByCountryQuery(string CountryCode) : IRequest<IReadOnlyList<PaymentProviderInfoDto>>;

