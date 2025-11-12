using Payment.Domain.Enums;
using Payment.Domain.ValueObjects;
using System.Text.Json;

namespace Payment.Domain.Entities;

/// <summary>
/// Entity representing a security incident tracking record.
/// Follows Clean Architecture - domain entity in Domain layer.
/// Stateless by design - persisted to database for Kubernetes deployment.
/// </summary>
public class SecurityIncident : Entity
{
    private SecurityIncident() { } // EF Core

    public SecurityIncident(
        Guid id,
        SecurityIncidentId incidentId,
        SecurityEvent securityEvent,
        SecurityIncidentSeverity severity,
        SecurityThreatType threatType,
        IEnumerable<string> affectedResources,
        IEnumerable<string> compromisedCredentials,
        ContainmentStrategy recommendedContainment,
        string remediationActionsJson,
        DateTime createdAt)
    {
        if (incidentId == null)
            throw new ArgumentNullException(nameof(incidentId));
        if (securityEvent == null)
            throw new ArgumentNullException(nameof(securityEvent));

        Id = id;
        IncidentId = incidentId.Value;
        SecurityEventType = securityEvent.EventType;
        SecurityEventTimestamp = securityEvent.Timestamp;
        SecurityEventUserId = securityEvent.UserId;
        SecurityEventIpAddress = securityEvent.IpAddress;
        SecurityEventResource = securityEvent.Resource;
        SecurityEventAction = securityEvent.Action;
        SecurityEventSucceeded = securityEvent.Succeeded;
        SecurityEventDetails = securityEvent.Details;
        Severity = severity;
        ThreatType = threatType;
        AffectedResources = JsonSerializer.Serialize(affectedResources ?? Enumerable.Empty<string>());
        CompromisedCredentials = JsonSerializer.Serialize(compromisedCredentials ?? Enumerable.Empty<string>());
        RecommendedContainment = recommendedContainment;
        RemediationActions = remediationActionsJson ?? "[]";
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid IncidentId { get; private set; }
    public SecurityEventType SecurityEventType { get; private set; }
    public DateTime SecurityEventTimestamp { get; private set; }
    public string? SecurityEventUserId { get; private set; }
    public string? SecurityEventIpAddress { get; private set; }
    public string SecurityEventResource { get; private set; } = string.Empty;
    public string SecurityEventAction { get; private set; } = string.Empty;
    public bool SecurityEventSucceeded { get; private set; }
    public string? SecurityEventDetails { get; private set; }
    public SecurityIncidentSeverity Severity { get; private set; }
    public SecurityThreatType ThreatType { get; private set; }
    public string AffectedResources { get; private set; } = string.Empty;
    public string CompromisedCredentials { get; private set; } = string.Empty;
    public ContainmentStrategy RecommendedContainment { get; private set; }
    public string RemediationActions { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ContainedAt { get; private set; }
    public ContainmentStrategy? ContainmentStrategy { get; private set; }

    public void MarkAsContained(ContainmentStrategy strategy, DateTime containedAt)
    {
        ContainedAt = containedAt;
        ContainmentStrategy = strategy;
    }

    public SecurityEvent ToSecurityEvent() =>
        SecurityEvent.Create(
            SecurityEventType,
            SecurityEventTimestamp,
            SecurityEventUserId,
            SecurityEventIpAddress,
            SecurityEventResource,
            SecurityEventAction,
            SecurityEventSucceeded,
            SecurityEventDetails);
}

