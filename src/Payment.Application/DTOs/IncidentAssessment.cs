namespace Payment.Application.DTOs;

/// <summary>
/// Represents an assessment of a payment incident.
/// Immutable record following DDD value object pattern.
/// </summary>
public sealed record IncidentAssessment(
    Domain.Enums.IncidentSeverity Severity,
    string RootCause,
    IEnumerable<string> AffectedProviders,
    int AffectedPaymentCount,
    TimeSpan EstimatedResolutionTime,
    IEnumerable<RecommendedAction> RecommendedActions)
{
    public static IncidentAssessment Create(
        Domain.Enums.IncidentSeverity severity,
        string rootCause,
        IEnumerable<string> affectedProviders,
        int affectedPaymentCount,
        TimeSpan estimatedResolutionTime,
        IEnumerable<RecommendedAction> recommendedActions) =>
        new(
            Severity: severity,
            RootCause: rootCause ?? throw new ArgumentNullException(nameof(rootCause)),
            AffectedProviders: affectedProviders ?? Enumerable.Empty<string>(),
            AffectedPaymentCount: affectedPaymentCount,
            EstimatedResolutionTime: estimatedResolutionTime,
            RecommendedActions: recommendedActions ?? Enumerable.Empty<RecommendedAction>());
}

