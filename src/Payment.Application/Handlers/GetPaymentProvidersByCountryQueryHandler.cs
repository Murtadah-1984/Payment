using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Application.DTOs;
using Payment.Application.Queries;
using Payment.Domain.ValueObjects;

namespace Payment.Application.Handlers;

/// <summary>
/// Handler for getting payment providers by country code.
/// Follows Clean Architecture - Application layer handler with OpenTelemetry tracing.
/// Supports configuration from appsettings.json or external provider registry.
/// </summary>
public sealed class GetPaymentProvidersByCountryQueryHandler 
    : IRequestHandler<GetPaymentProvidersByCountryQuery, IReadOnlyList<PaymentProviderInfoDto>>
{
    private static readonly ActivitySource ActivitySource = new("Payment.Application");
    
    private readonly ILogger<GetPaymentProvidersByCountryQueryHandler> _logger;
    private readonly PaymentProviderCatalogOptions? _options;

    public GetPaymentProvidersByCountryQueryHandler(
        ILogger<GetPaymentProvidersByCountryQueryHandler> logger,
        IOptions<PaymentProviderCatalogOptions>? options = null)
    {
        _logger = logger;
        _options = options?.Value;
    }

    public Task<IReadOnlyList<PaymentProviderInfoDto>> Handle(
        GetPaymentProvidersByCountryQuery request, 
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("GetPaymentProvidersByCountry");
        activity?.SetTag("country.code", request.CountryCode);

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

            var providers = PaymentProviderCatalog.GetProvidersByCountry(request.CountryCode);
            
            activity?.SetTag("providers.count", providers.Count);
            activity?.SetTag("country.supported", providers.Count > 0);
            activity?.SetStatus(providers.Count > 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

            if (providers.Count == 0)
            {
                _logger.LogWarning("No active payment providers found for country code {CountryCode}", request.CountryCode);
            }
            else
            {
                _logger.LogInformation("Found {Count} active payment providers for country {CountryCode}", 
                    providers.Count, request.CountryCode);
            }

            var dtos = providers.Select(p => new PaymentProviderInfoDto(
                p.ProviderName,
                p.CountryCode,
                p.Currency,
                p.PaymentMethod,
                p.IsActive)).ToList();

            return Task.FromResult<IReadOnlyList<PaymentProviderInfoDto>>(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment providers for country {CountryCode}", request.CountryCode);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }
}

/// <summary>
/// Configuration options for PaymentProviderCatalog.
/// Allows loading providers from appsettings.json.
/// </summary>
public class PaymentProviderCatalogOptions
{
    public const string SectionName = "PaymentProviderCatalog";

    public List<PaymentProviderInfoConfiguration> Providers { get; set; } = new();
}

/// <summary>
/// Configuration model for payment provider info from appsettings.json.
/// </summary>
public class PaymentProviderInfoConfiguration
{
    public string ProviderName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

