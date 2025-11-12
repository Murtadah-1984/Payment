using System.Collections.Frozen;

namespace Payment.Domain.ValueObjects;

/// <summary>
/// Static catalog of payment providers grouped by country.
/// Follows Clean Architecture - Domain layer with no external dependencies.
/// Supports configuration via appsettings.json or external provider registry.
/// </summary>
public static class PaymentProviderCatalog
{
    /// <summary>
    /// Static catalog of providers grouped by country code (ISO 3166-1 alpha-2).
    /// This is the default catalog; can be overridden by configuration.
    /// </summary>
    private static readonly Dictionary<string, List<PaymentProviderInfo>> DefaultCatalog = new()
    {
        ["IQ"] = new List<PaymentProviderInfo>
        {
            new("ZainCash", "IQ", "IQD", "Wallet", true),
            new("ZainCash", "IQ", "USD", "Wallet", true),
            new("FIB", "IQ", "IQD", "Card", true),
            new("FIB", "IQ", "USD", "Card", true),
            new("Telr", "IQ", "IQD", "Card", true),
            new("Telr", "IQ", "USD", "Card", true),
            new("Paytabs", "IQ", "IQD", "Card", true),
            new("Paytabs", "IQ", "USD", "Card", true),
            new("Tap", "IQ", "IQD", "Card", true),
            new("Tap", "IQ", "USD", "Card", true)
        },
        ["KW"] = new List<PaymentProviderInfo>
        {
            new("Telr", "KW", "KWD", "Card", true),
            new("Paytabs", "KW", "KWD", "Card", true),
            new("Tap", "KW", "KWD", "Card", true),
            new("AmazonPaymentServices", "KW", "KWD", "Card", true),
            new("Checkout", "KW", "KWD", "Card", true),
            new("Stripe", "KW", "KWD", "Card", true)
        },
        ["AE"] = new List<PaymentProviderInfo>
        {
            new("Telr", "AE", "AED", "Card", true),
            new("Paytabs", "AE", "AED", "Card", true),
            new("Tap", "AE", "AED", "Card", true),
            new("AmazonPaymentServices", "AE", "AED", "Card", true),
            new("Checkout", "AE", "AED", "Card", true),
            new("Stripe", "AE", "AED", "Card", true),
            new("Verifone", "AE", "AED", "Card", true)
        },
        ["SA"] = new List<PaymentProviderInfo>
        {
            new("Paytabs", "SA", "SAR", "Card", true),
            new("Tap", "SA", "SAR", "Card", true),
            new("AmazonPaymentServices", "SA", "SAR", "Card", true),
            new("Checkout", "SA", "SAR", "Card", true),
            new("Stripe", "SA", "SAR", "Card", true)
        },
        ["BH"] = new List<PaymentProviderInfo>
        {
            new("Telr", "BH", "BHD", "Card", true),
            new("Paytabs", "BH", "BHD", "Card", true),
            new("Tap", "BH", "BHD", "Card", true),
            new("AmazonPaymentServices", "BH", "BHD", "Card", true),
            new("Stripe", "BH", "BHD", "Card", true)
        },
        ["OM"] = new List<PaymentProviderInfo>
        {
            new("Telr", "OM", "OMR", "Card", true),
            new("Paytabs", "OM", "OMR", "Card", true),
            new("Tap", "OM", "OMR", "Card", true),
            new("AmazonPaymentServices", "OM", "OMR", "Card", true),
            new("Stripe", "OM", "OMR", "Card", true)
        },
        ["QA"] = new List<PaymentProviderInfo>
        {
            new("Telr", "QA", "QAR", "Card", true),
            new("Paytabs", "QA", "QAR", "Card", true),
            new("Tap", "QA", "QAR", "Card", true),
            new("AmazonPaymentServices", "QA", "QAR", "Card", true),
            new("Checkout", "QA", "QAR", "Card", true),
            new("Stripe", "QA", "QAR", "Card", true)
        }
    };

    private static FrozenDictionary<string, List<PaymentProviderInfo>>? _catalog;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the current catalog. If Initialize has been called, returns the configured catalog;
    /// otherwise returns the default catalog.
    /// </summary>
    public static IReadOnlyDictionary<string, List<PaymentProviderInfo>> Catalog
    {
        get
        {
            if (_catalog == null)
            {
                lock (_lock)
                {
                    _catalog ??= DefaultCatalog.ToFrozenDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value,
                        StringComparer.OrdinalIgnoreCase);
                }
            }
            return _catalog;
        }
    }

    /// <summary>
    /// Initializes the catalog with custom provider data.
    /// This allows loading from appsettings.json or external provider registry.
    /// Thread-safe initialization.
    /// </summary>
    public static void Initialize(IEnumerable<PaymentProviderInfo> providers)
    {
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));

        var groupedProviders = providers
            .Where(p => p.IsValid())
            .GroupBy(p => p.CountryCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        lock (_lock)
        {
            _catalog = groupedProviders.ToFrozenDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Resets the catalog to the default catalog.
    /// Useful for testing or reverting to defaults.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _catalog = DefaultCatalog.ToFrozenDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Gets all active providers for a specific country code.
    /// Returns empty list if country not found or no active providers.
    /// </summary>
    public static IReadOnlyList<PaymentProviderInfo> GetProvidersByCountry(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return Array.Empty<PaymentProviderInfo>();

        if (!Catalog.TryGetValue(countryCode, out var providers))
            return Array.Empty<PaymentProviderInfo>();

        return providers.Where(p => p.IsActive).ToList();
    }

    /// <summary>
    /// Gets all providers (active and inactive) for a specific country code.
    /// Returns empty list if country not found.
    /// </summary>
    public static IReadOnlyList<PaymentProviderInfo> GetAllProvidersByCountry(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return Array.Empty<PaymentProviderInfo>();

        if (!Catalog.TryGetValue(countryCode, out var providers))
            return Array.Empty<PaymentProviderInfo>();

        return providers;
    }

    /// <summary>
    /// Checks if a country code is supported.
    /// </summary>
    public static bool IsCountrySupported(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return false;

        return Catalog.ContainsKey(countryCode);
    }

    /// <summary>
    /// Gets all supported country codes.
    /// </summary>
    public static IReadOnlyList<string> GetSupportedCountries()
    {
        return Catalog.Keys.ToList();
    }

    /// <summary>
    /// Gets all active providers from all countries.
    /// Returns a flattened list of all active payment providers across all countries.
    /// </summary>
    public static IReadOnlyList<PaymentProviderInfo> GetAll()
    {
        return Catalog.Values
            .SelectMany(providers => providers)
            .Where(p => p.IsActive)
            .ToList();
    }
}

