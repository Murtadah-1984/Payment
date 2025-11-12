using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Application.Common;
using Payment.Application.DTOs;
using Payment.Application.Queries;
using Payment.Domain.ValueObjects;

namespace Payment.Application.Handlers;

/// <summary>
/// Handler for getting payment providers with optional filtering.
/// Follows Clean Architecture - Application layer handler with OpenTelemetry tracing.
/// Supports filtering by country, currency, and payment method.
/// </summary>
public sealed class GetProvidersHandler 
    : IRequestHandler<GetProvidersQuery, IEnumerable<PaymentProviderInfoDto>>
{
    private static readonly ActivitySource ActivitySource = new("Payment.Application");
    
    private readonly ILogger<GetProvidersHandler> _logger;
    private readonly PaymentProviderCatalogOptions? _options;

    public GetProvidersHandler(
        ILogger<GetProvidersHandler> logger,
        IOptions<PaymentProviderCatalogOptions>? options = null)
    {
        _logger = logger;
        _options = options?.Value;
    }

    public Task<IEnumerable<PaymentProviderInfoDto>> Handle(
        GetProvidersQuery query, 
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("GetProviders");
        activity?.SetTag("query.country", query.Country ?? "all");
        activity?.SetTag("query.currency", query.Currency ?? "all");
        activity?.SetTag("query.method", query.Method ?? "all");

        try
        {
            // Load from configuration if available
            if (_options != null && _options.Providers != null && _options.Providers.Any())
            {
                var configuredProviders = _options.Providers
                    .Select(p => new PaymentProviderInfo(
                        p.ProviderName,
                        p.CountryCode,
                        p.Currency,
                        p.PaymentMethod,
                        p.IsActive))
                    .ToList();

                PaymentProviderCatalog.Initialize(configuredProviders);
                _logger.LogDebug("Payment provider catalog initialized from configuration");
            }

            // Get all active providers from the catalog
            var all = PaymentProviderCatalog.GetAll();

            // Apply filters
            var filtered = all.Where(p =>
                (string.IsNullOrEmpty(query.Country) || 
                 p.CountryCode.Equals(query.Country, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(query.Currency) || 
                 p.Currency.Equals(query.Currency, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(query.Method) || 
                 p.PaymentMethod.Equals(query.Method, StringComparison.OrdinalIgnoreCase))
            );

            var result = filtered.Select(p => new PaymentProviderInfoDto(
                p.ProviderName,
                p.CountryCode,
                p.Currency,
                p.PaymentMethod,
                p.IsActive)).ToList();

            activity?.SetTag("providers.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation(
                "Found {Count} payment providers matching filters: Country={Country}, Currency={Currency}, Method={Method}",
                result.Count, query.Country ?? "all", query.Currency ?? "all", query.Method ?? "all");

            return Task.FromResult<IEnumerable<PaymentProviderInfoDto>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment providers with filters");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }
}

