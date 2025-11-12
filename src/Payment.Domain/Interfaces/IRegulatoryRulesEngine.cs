using Payment.Domain.Entities;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for regulatory compliance rules engine.
/// Follows Interface Segregation Principle - focused on compliance validation.
/// </summary>
public interface IRegulatoryRulesEngine
{
    /// <summary>
    /// Gets the compliance rule for a specific country code.
    /// </summary>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code (e.g., "KW", "SA", "IQ").</param>
    /// <returns>The compliance rule if found, null otherwise.</returns>
    ComplianceRule? GetRule(string countryCode);

    /// <summary>
    /// Validates a payment transaction against regulatory rules for the specified country.
    /// </summary>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code.</param>
    /// <param name="payment">The payment entity to validate.</param>
    /// <returns>True if the transaction complies with regulations, false otherwise.</returns>
    bool ValidateTransaction(string countryCode, Entities.Payment payment);
}

