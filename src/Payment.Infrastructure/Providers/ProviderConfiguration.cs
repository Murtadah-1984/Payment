namespace Payment.Infrastructure.Providers;

public class ProviderConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string MerchantSecret { get; set; } = string.Empty;
    public bool IsTestMode { get; set; } = true;
}

public class ZainCashProviderConfiguration : ProviderConfiguration
{
    public string Msisdn { get; set; } = string.Empty; // Wallet phone number
    public string RedirectUrl { get; set; } = string.Empty; // URL to redirect after payment
    public string ServiceType { get; set; } = "Payment"; // Service type identifier
}

public class FibProviderConfiguration : ProviderConfiguration
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string StatusCallbackUrl { get; set; } = string.Empty;
}

public class StripeProviderConfiguration : ProviderConfiguration
{
    public string PublishableKey { get; set; } = string.Empty;
}

public class SquareProviderConfiguration : ProviderConfiguration
{
    public string AccessToken { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
}

public class HelcimProviderConfiguration : ProviderConfiguration
{
    public string ApiToken { get; set; } = string.Empty;
}

public class AmazonProviderConfiguration : ProviderConfiguration
{
    public string AccessCode { get; set; } = string.Empty;
    public string MerchantIdentifier { get; set; } = string.Empty;
    public string ShaRequestPhrase { get; set; } = string.Empty;
    public string ShaResponsePhrase { get; set; } = string.Empty;
    public string ShaType { get; set; } = "SHA256";
    public string Language { get; set; } = "en";
    public string ReturnUrl { get; set; } = string.Empty;
}

public class TelrProviderConfiguration : ProviderConfiguration
{
    public string StoreId { get; set; } = string.Empty;
    public string AuthKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string DeclineUrl { get; set; } = string.Empty;
}

public class CheckoutProviderConfiguration : ProviderConfiguration
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool UseOAuth { get; set; } = false;
}

public class VerifoneProviderConfiguration : ProviderConfiguration
{
    public string SellerId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
}

public class PaytabsProviderConfiguration : ProviderConfiguration
{
    public string ProfileId { get; set; } = string.Empty;
    public string ServerKey { get; set; } = string.Empty;
    public string ClientKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
}

public class TapProviderConfiguration : ProviderConfiguration
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
}

public class TapToPayProviderConfiguration : ProviderConfiguration
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public bool ReplayPreventionEnabled { get; set; } = true; // Enable replay prevention by default
}

