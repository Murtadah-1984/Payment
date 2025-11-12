namespace Payment.Application.Common;

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

