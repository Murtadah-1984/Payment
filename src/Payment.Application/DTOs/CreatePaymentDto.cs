namespace Payment.Application.DTOs;

/// <summary>
/// Represents a request to initiate a payment through the Payment Microservice.
/// Stateless by design and suitable for multi-tenant, provider-agnostic processing.
/// </summary>
public sealed record CreatePaymentDto(
    Guid RequestId,                       // For traceability
    decimal Amount,
    string Currency,
    string PaymentMethod,                 // e.g. "Wallet", "Card", "Cash"
    string Provider,                      // e.g. "ZainCash", "Stripe"
    string MerchantId,                    // Owning merchant (or service owner)
    string OrderId,                       // External order reference
    string ProjectCode,                   // Identifies the project or tenant
    string IdempotencyKey,                // For idempotency - prevents duplicate payments from retries
    decimal? SystemFeePercent = null,     // Optional override; if null, fetched from Config Service
    SplitRuleDto? SplitRule = null,       // Optional explicit rule (multi-account split)
    Dictionary<string, string>? Metadata = null,
    string? CallbackUrl = null,           // Optional: Provider webhook for async confirmation
    string? CustomerEmail = null,         // For receipts / provider requirements
    string? CustomerPhone = null,        // For wallet-based providers
    string? NfcToken = null,             // Tokenized NFC payload from mobile SDK (Apple Pay, Google Pay, Tap SDK)
    string? DeviceId = null,              // Device or terminal ID for Tap-to-Pay transactions
    string? CustomerId = null,             // Customer identifier for Tap-to-Pay transactions
    string? CountryCode = null            // ISO 3166-1 alpha-2 country code (e.g., "KW", "SA", "IQ") for regulatory compliance
);
