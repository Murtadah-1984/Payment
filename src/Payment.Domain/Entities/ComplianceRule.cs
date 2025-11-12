namespace Payment.Domain.Entities;

/// <summary>
/// Represents a regulatory compliance rule for a specific country.
/// Immutable record following Domain-Driven Design principles.
/// </summary>
public sealed record ComplianceRule(
    string CountryCode,
    string RegulationName,
    string Description,
    bool Requires3DSecure,
    bool RequiresEncryption,
    bool RequiresSettlementReport);

