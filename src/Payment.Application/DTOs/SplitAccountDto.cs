namespace Payment.Application.DTOs;

/// <summary>
/// Represents a single account in a split payment configuration.
/// </summary>
public sealed record SplitAccountDto(
    string AccountType,        // e.g. "SystemOwner", "ServiceOwner", "Partner", "VendorOwner"
    string AccountIdentifier,  // e.g. IBAN, Wallet ID, Stripe Account ID, etc.
    decimal Percentage         // Must total 100% across all accounts
);

