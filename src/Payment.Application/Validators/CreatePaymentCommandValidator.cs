using FluentValidation;
using Payment.Application.Commands;
using System.Text.RegularExpressions;

namespace Payment.Application.Validators;

/// <summary>
/// Validator for CreatePaymentCommand with strict input validation and sanitization.
/// Implements CRITICAL security measures to prevent XSS, SQL injection, and data overflow attacks.
/// </summary>
public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    // Valid ISO 4217 currency codes (common ones)
    private static readonly HashSet<string> ValidCurrencyCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "CNY", "INR", "BRL",
        "ZAR", "MXN", "SGD", "HKD", "NOK", "SEK", "KRW", "TRY", "RUB", "NZD",
        "AED", "SAR", "IQD", "JOD", "KWD", "BHD", "OMR", "QAR", "EGP", "ILS"
    };

    private const int MaxAmount = 1_000_000; // Prevent overflow attacks
    private const int MaxMetadataKeys = 50;
    private const int MaxMetadataValueLength = 1000; // 1KB per value
    private const int MaxMerchantIdLength = 100;
    private const int MaxOrderIdLength = 100;
    private const int MaxProjectCodeLength = 100;
    private const int MaxCallbackUrlLength = 2048;
    private const int MaxCustomerPhoneLength = 20;

    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty()
            .WithMessage("Request ID is required for traceability");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency key is required")
            .MinimumLength(16)
            .WithMessage("Idempotency key must be at least 16 characters")
            .MaximumLength(128)
            .WithMessage("Idempotency key must not exceed 128 characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Idempotency key must contain only alphanumeric characters, hyphens, and underscores");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero")
            .LessThanOrEqualTo(MaxAmount)
            .WithMessage($"Amount must not exceed {MaxAmount:N0} to prevent overflow attacks");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .Length(3)
            .WithMessage("Currency must be a 3-letter code")
            .Must(BeValidCurrency)
            .WithMessage("Currency must be a valid ISO 4217 currency code");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .WithMessage("Payment method is required")
            .Must(method => new[] { "CreditCard", "DebitCard", "PayPal", "BankTransfer", "Crypto", "Wallet", "Card", "Cash", "TapToPay" }
                .Contains(method, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Invalid payment method");

        RuleFor(x => x.Provider)
            .NotEmpty()
            .WithMessage("Payment provider is required")
            .Must(provider => new[] { "ZainCash", "AsiaHawala", "Stripe", "FIB", "Square", "Helcim", "AmazonPaymentServices", "Telr", "Checkout", "Verifone", "Paytabs", "Tap", "TapToPay" }
                .Contains(provider, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Invalid payment provider. Supported providers: ZainCash, AsiaHawala, Stripe, FIB, Square, Helcim, AmazonPaymentServices, Telr, Checkout, Verifone, Paytabs, Tap, TapToPay");

        RuleFor(x => x.SystemFeePercent)
            .InclusiveBetween(0, 100)
            .When(x => x.SystemFeePercent.HasValue)
            .WithMessage("System fee percent must be between 0 and 100");

        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("Merchant ID is required")
            .MaximumLength(MaxMerchantIdLength)
            .WithMessage($"Merchant ID must not exceed {MaxMerchantIdLength} characters")
            .Must(NotContainSpecialCharacters)
            .WithMessage("Merchant ID contains invalid characters. Only alphanumeric characters, hyphens, underscores, and dots are allowed");

        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required")
            .MaximumLength(MaxOrderIdLength)
            .WithMessage($"Order ID must not exceed {MaxOrderIdLength} characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Order ID must be alphanumeric with hyphens/underscores only");

        RuleFor(x => x.ProjectCode)
            .NotEmpty()
            .WithMessage("Project code is required")
            .MaximumLength(MaxProjectCodeLength)
            .WithMessage($"Project code must not exceed {MaxProjectCodeLength} characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Project code must be alphanumeric with hyphens/underscores only");

        RuleFor(x => x.SplitRule)
            .Must(sr => sr == null || (sr.Accounts != null && sr.Accounts.Count > 0))
            .WithMessage("Split rule must contain at least one account")
            .Must(sr => sr == null || Math.Abs(sr.Accounts!.Sum(a => a.Percentage) - 100m) <= 0.01m)
            .WithMessage("Split percentages must total 100%")
            .When(x => x.SplitRule != null);

        RuleForEach(x => x.SplitRule!.Accounts!)
            .ChildRules(account =>
            {
                account.RuleFor(a => a.AccountType)
                    .NotEmpty()
                    .WithMessage("Account type is required")
                    .MaximumLength(50)
                    .WithMessage("Account type must not exceed 50 characters");

                account.RuleFor(a => a.AccountIdentifier)
                    .NotEmpty()
                    .WithMessage("Account identifier is required")
                    .MaximumLength(200)
                    .WithMessage("Account identifier must not exceed 200 characters")
                    .Must(NotContainDangerousContent)
                    .WithMessage("Account identifier contains potentially dangerous content");

                account.RuleFor(a => a.Percentage)
                    .GreaterThan(0)
                    .LessThanOrEqualTo(100)
                    .WithMessage("Account percentage must be between 0 and 100");
            })
            .When(x => x.SplitRule != null && x.SplitRule.Accounts != null);

        RuleFor(x => x.Metadata)
            .Must(HaveValidMetadata)
            .When(x => x.Metadata != null)
            .WithMessage("Metadata exceeds size limits or contains invalid characters");

        RuleFor(x => x.CallbackUrl)
            .Must(BeValidUrl)
            .When(x => !string.IsNullOrEmpty(x.CallbackUrl))
            .WithMessage("Callback URL must be a valid HTTPS URL")
            .MaximumLength(MaxCallbackUrlLength)
            .When(x => !string.IsNullOrEmpty(x.CallbackUrl))
            .WithMessage($"Callback URL must not exceed {MaxCallbackUrlLength} characters");

        RuleFor(x => x.CustomerEmail)
            .Must(BeValidEmail)
            .When(x => !string.IsNullOrEmpty(x.CustomerEmail))
            .WithMessage("Customer email must be a valid email address")
            .MaximumLength(254) // RFC 5321 limit
            .When(x => !string.IsNullOrEmpty(x.CustomerEmail))
            .WithMessage("Customer email must not exceed 254 characters");

        RuleFor(x => x.CustomerPhone)
            .MaximumLength(MaxCustomerPhoneLength)
            .When(x => !string.IsNullOrEmpty(x.CustomerPhone))
            .WithMessage($"Customer phone must not exceed {MaxCustomerPhoneLength} characters")
            .Must(BeValidPhoneNumber)
            .When(x => !string.IsNullOrEmpty(x.CustomerPhone))
            .WithMessage("Customer phone must be a valid phone number format");

        // Tap-to-Pay specific validations
        RuleFor(x => x.NfcToken)
            .NotEmpty()
            .When(x => string.Equals(x.Provider, "TapToPay", StringComparison.OrdinalIgnoreCase) || 
                       string.Equals(x.PaymentMethod, "TapToPay", StringComparison.OrdinalIgnoreCase))
            .WithMessage("NFC token is required for Tap-to-Pay transactions")
            .MaximumLength(5000) // JWT tokens can be long
            .When(x => !string.IsNullOrEmpty(x.NfcToken))
            .WithMessage("NFC token must not exceed 5000 characters");

        RuleFor(x => x.DeviceId)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.DeviceId))
            .WithMessage("Device ID must not exceed 200 characters")
            .Must(NotContainSpecialCharacters)
            .When(x => !string.IsNullOrEmpty(x.DeviceId))
            .WithMessage("Device ID contains invalid characters");

        RuleFor(x => x.CustomerId)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.CustomerId))
            .WithMessage("Customer ID must not exceed 200 characters")
            .Must(NotContainSpecialCharacters)
            .When(x => !string.IsNullOrEmpty(x.CustomerId))
            .WithMessage("Customer ID contains invalid characters");
    }

    private static bool BeValidCurrency(string currency)
    {
        return ValidCurrencyCodes.Contains(currency);
    }

    private static bool NotContainSpecialCharacters(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return true;

        // Allow alphanumeric, hyphens, underscores, and dots (for merchant IDs like "merchant.com")
        return Regex.IsMatch(value, "^[a-zA-Z0-9._-]+$");
    }

    private static bool HaveValidMetadata(Dictionary<string, string>? metadata)
    {
        if (metadata == null)
            return true;

        // Limit number of keys
        if (metadata.Count > MaxMetadataKeys)
            return false;

        foreach (var kvp in metadata)
        {
            // Key validation
            if (string.IsNullOrEmpty(kvp.Key))
                return false;

            if (kvp.Key.Length > 100)
                return false;

            if (!Regex.IsMatch(kvp.Key, "^[a-zA-Z0-9_-]+$"))
                return false;

            // Value validation
            if (kvp.Value.Length > MaxMetadataValueLength)
                return false;

            // Prevent script injection
            if (ContainsDangerousContent(kvp.Value))
                return false;
        }

        return true;
    }

    private static bool ContainsDangerousContent(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var dangerousPatterns = new[]
        {
            "<script",
            "javascript:",
            "onerror=",
            "onclick=",
            "onload=",
            "onmouseover=",
            "vbscript:",
            "data:text/html",
            "&#x",
            "&#60;",
            "eval(",
            "expression("
        };

        var lowerValue = value.ToLowerInvariant();
        return dangerousPatterns.Any(pattern => lowerValue.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static bool NotContainDangerousContent(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return true;

        return !ContainsDangerousContent(value);
    }

    private static bool BeValidUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return true;

        // Must be HTTPS for security
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps) &&
               !string.IsNullOrEmpty(uri.Host);
    }

    private static bool BeValidPhoneNumber(string? phone)
    {
        if (string.IsNullOrEmpty(phone))
            return true;

        // Allow international format: +1234567890, (123) 456-7890, 123-456-7890, etc.
        // Must contain only digits, spaces, hyphens, parentheses, dots, and plus sign
        // More flexible pattern to handle various international formats
        var cleaned = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace(".", "");
        
        // Must contain at least 7 digits (minimum phone number length)
        if (cleaned.Count(char.IsDigit) < 7)
            return false;

        // Must start with + or digit, and contain only valid characters
        return Regex.IsMatch(phone, @"^[\+]?[0-9\s\-\(\)\.]{7,}$");
    }

    private static bool BeValidEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return true;

        // Basic email validation - must have @ and valid domain
        if (!email.Contains('@'))
            return false;

        var parts = email.Split('@');
        if (parts.Length != 2)
            return false;

        var localPart = parts[0];
        var domain = parts[1];

        // Local part cannot be empty
        if (string.IsNullOrEmpty(localPart))
            return false;

        // Domain cannot be empty and must contain at least one dot
        if (string.IsNullOrEmpty(domain) || !domain.Contains('.'))
            return false;

        // Domain cannot start or end with dot
        if (domain.StartsWith('.') || domain.EndsWith('.'))
            return false;

        // Use System.Net.Mail.MailAddress for validation
        try
        {
            var mailAddress = new System.Net.Mail.MailAddress(email);
            return mailAddress.Address == email; // Ensure no normalization occurred
        }
        catch
        {
            return false;
        }
    }
}

