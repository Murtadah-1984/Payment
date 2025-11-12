using Microsoft.Extensions.Logging;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.Enums;

namespace Payment.Domain.Services;

/// <summary>
/// Regulatory compliance rules engine.
/// Enforces per-country compliance and regulation rules (e.g., CBK in Kuwait, SAMA in Saudi Arabia, PCI DSS, etc.).
/// Follows Single Responsibility Principle - only handles regulatory compliance validation.
/// </summary>
public sealed class RegulatoryRulesEngine : IRegulatoryRulesEngine
{
    private readonly IEnumerable<ComplianceRule> _rules;
    private readonly ILogger<RegulatoryRulesEngine> _logger;

    public RegulatoryRulesEngine(
        IEnumerable<ComplianceRule> rules,
        ILogger<RegulatoryRulesEngine> logger)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ComplianceRule? GetRule(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        return _rules.FirstOrDefault(r => 
            r.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public bool ValidateTransaction(string countryCode, Entities.Payment payment)
    {
        if (payment == null)
            throw new ArgumentNullException(nameof(payment));

        var rule = GetRule(countryCode);
        if (rule == null)
        {
            _logger.LogDebug("No compliance rule found for country {CountryCode}, allowing transaction", countryCode);
            return true; // No restriction - allow transaction
        }

        _logger.LogInformation(
            "Validating payment {PaymentId} against compliance rule {RegulationName} for country {CountryCode}",
            payment.Id.Value, rule.RegulationName, countryCode);

        // Validate 3D Secure requirement
        if (rule.Requires3DSecure)
        {
            var has3DSecure = payment.ThreeDSecureStatus == ThreeDSecureStatus.Authenticated ||
                             payment.ThreeDSecureStatus == ThreeDSecureStatus.Skipped;

            if (!has3DSecure)
            {
                _logger.LogWarning(
                    "Payment {PaymentId} rejected due to missing 3D Secure authentication (Regulation: {RegulationName}, Country: {CountryCode})",
                    payment.Id.Value, rule.RegulationName, countryCode);
                return false;
            }
        }

        // Additional validations can be added here:
        // - Encryption validation (if payment data is encrypted)
        // - Settlement report requirements
        // - Other country-specific rules

        _logger.LogInformation(
            "Payment {PaymentId} passed compliance validation for {RegulationName} (Country: {CountryCode})",
            payment.Id.Value, rule.RegulationName, countryCode);

        return true;
    }
}

