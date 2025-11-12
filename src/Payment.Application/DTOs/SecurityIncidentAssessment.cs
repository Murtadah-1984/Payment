using Payment.Domain.Enums;

namespace Payment.Application.DTOs;

/// <summary>
/// Represents an assessment of a security incident.
/// Immutable record following DDD value object pattern.
/// </summary>
public sealed record SecurityIncidentAssessment(
    SecurityIncidentSeverity Severity,
    SecurityThreatType ThreatType,
    IEnumerable<string> AffectedResources,
    IEnumerable<string> CompromisedCredentials,
    ContainmentStrategy RecommendedContainment,
    IEnumerable<RemediationAction> RemediationActions)
{
    public static SecurityIncidentAssessment Create(
        SecurityIncidentSeverity severity,
        SecurityThreatType threatType,
        IEnumerable<string> affectedResources,
        IEnumerable<string> compromisedCredentials,
        ContainmentStrategy recommendedContainment,
        IEnumerable<RemediationAction> remediationActions) =>
        new(
            Severity: severity,
            ThreatType: threatType,
            AffectedResources: affectedResources ?? Enumerable.Empty<string>(),
            CompromisedCredentials: compromisedCredentials ?? Enumerable.Empty<string>(),
            RecommendedContainment: recommendedContainment,
            RemediationActions: remediationActions ?? Enumerable.Empty<RemediationAction>());
}

