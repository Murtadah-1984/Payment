using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using System.Text.Json;

namespace Payment.Application.Services;

/// <summary>
/// Service for responding to security incidents.
/// Follows SOLID principles - single responsibility for security incident response.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class SecurityIncidentResponseService : ISecurityIncidentResponseService
{
    private readonly IAuditLogger _auditLogger;
    private readonly ICredentialRevocationService _credentialRevocationService;
    private readonly ISecurityNotificationService _securityNotificationService;
    private readonly ISecurityIncidentRepository _securityIncidentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SecurityIncidentResponseService> _logger;

    public SecurityIncidentResponseService(
        IAuditLogger auditLogger,
        ICredentialRevocationService credentialRevocationService,
        ISecurityNotificationService securityNotificationService,
        ISecurityIncidentRepository securityIncidentRepository,
        IUnitOfWork unitOfWork,
        ILogger<SecurityIncidentResponseService> logger)
    {
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _credentialRevocationService = credentialRevocationService ?? throw new ArgumentNullException(nameof(credentialRevocationService));
        _securityNotificationService = securityNotificationService ?? throw new ArgumentNullException(nameof(securityNotificationService));
        _securityIncidentRepository = securityIncidentRepository ?? throw new ArgumentNullException(nameof(securityIncidentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SecurityIncidentAssessment> AssessIncidentAsync(
        SecurityEvent securityEvent,
        CancellationToken cancellationToken = default)
    {
        if (securityEvent == null)
            throw new ArgumentNullException(nameof(securityEvent));

        _logger.LogInformation(
            "Assessing security incident. EventType: {EventType}, Resource: {Resource}, Action: {Action}, Succeeded: {Succeeded}",
            securityEvent.EventType, securityEvent.Resource, securityEvent.Action, securityEvent.Succeeded);

        // Query related security events from audit log
        var relatedEvents = await _auditLogger.QuerySecurityEventsAsync(
            userId: securityEvent.UserId,
            eventType: securityEvent.EventType,
            startDate: securityEvent.Timestamp.AddHours(-24),
            endDate: securityEvent.Timestamp.AddHours(1),
            cancellationToken: cancellationToken);

        // Determine severity based on event type and related events
        var severity = DetermineSeverity(securityEvent, relatedEvents);
        
        // Identify threat type
        var threatType = DetermineThreatType(securityEvent);
        
        // Identify affected resources
        var affectedResources = IdentifyAffectedResources(securityEvent, relatedEvents);
        
        // Identify compromised credentials
        var compromisedCredentials = IdentifyCompromisedCredentials(securityEvent, relatedEvents);
        
        // Determine recommended containment strategy
        var recommendedContainment = DetermineContainmentStrategy(securityEvent, severity, threatType);
        
        // Generate remediation actions
        var remediationActions = GenerateRemediationActions(securityEvent, severity, threatType, compromisedCredentials);

        var assessment = SecurityIncidentAssessment.Create(
            severity: severity,
            threatType: threatType,
            affectedResources: affectedResources,
            compromisedCredentials: compromisedCredentials,
            recommendedContainment: recommendedContainment,
            remediationActions: remediationActions);

        // Track the incident in database (stateless - suitable for Kubernetes)
        var incidentId = SecurityIncidentId.NewId();
        var remediationActionsJson = JsonSerializer.Serialize(remediationActions);
        var securityIncident = new Domain.Entities.SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: incidentId,
            securityEvent: securityEvent,
            severity: severity,
            threatType: threatType,
            affectedResources: affectedResources,
            compromisedCredentials: compromisedCredentials,
            recommendedContainment: recommendedContainment,
            remediationActionsJson: remediationActionsJson,
            createdAt: DateTime.UtcNow);

        await _securityIncidentRepository.AddAsync(securityIncident, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Security incident assessment completed. IncidentId: {IncidentId}, Severity: {Severity}, ThreatType: {ThreatType}, RecommendedContainment: {Containment}",
            incidentId.Value, severity, threatType, recommendedContainment);

        // Send security alert if severity is high or critical
        if (severity >= SecurityIncidentSeverity.High)
        {
            await _securityNotificationService.SendSecurityAlertAsync(
                severity: severity,
                title: $"Security Incident: {securityEvent.EventType}",
                message: $"Security incident detected: {securityEvent.Action} on {securityEvent.Resource}",
                metadata: new Dictionary<string, object>
                {
                    { "IncidentId", incidentId.Value },
                    { "EventType", securityEvent.EventType.ToString() },
                    { "Resource", securityEvent.Resource },
                    { "IpAddress", securityEvent.IpAddress ?? "Unknown" },
                    { "UserId", securityEvent.UserId ?? "Unknown" }
                },
                cancellationToken: cancellationToken);
        }

        return assessment;
    }

    public async Task ContainIncidentAsync(
        SecurityIncidentId incidentId,
        ContainmentStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        if (incidentId == null)
            throw new ArgumentNullException(nameof(incidentId));

        _logger.LogInformation(
            "Containing security incident. IncidentId: {IncidentId}, Strategy: {Strategy}",
            incidentId.Value, strategy);

        var securityIncident = await _securityIncidentRepository.GetByIncidentIdAsync(incidentId, cancellationToken);
        if (securityIncident == null)
        {
            _logger.LogWarning("Security incident not found: {IncidentId}", incidentId.Value);
            throw new InvalidOperationException($"Security incident {incidentId.Value} not found");
        }

        try
        {
            // Execute containment strategy
            var securityEvent = securityIncident.ToSecurityEvent();
            await ExecuteContainmentStrategyAsync(securityEvent, strategy, cancellationToken);

            // Update incident in database
            securityIncident.MarkAsContained(strategy, DateTime.UtcNow);
            await _securityIncidentRepository.UpdateAsync(securityIncident, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Security incident contained successfully. IncidentId: {IncidentId}, Strategy: {Strategy}",
                incidentId.Value, strategy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to contain security incident. IncidentId: {IncidentId}", incidentId.Value);
            throw;
        }
    }

    public async Task<string> GenerateIncidentReportAsync(
        SecurityIncidentId incidentId,
        CancellationToken cancellationToken = default)
    {
        if (incidentId == null)
            throw new ArgumentNullException(nameof(incidentId));

        _logger.LogInformation("Generating incident report. IncidentId: {IncidentId}", incidentId.Value);

        var securityIncident = await _securityIncidentRepository.GetByIncidentIdAsync(incidentId, cancellationToken);
        if (securityIncident == null)
        {
            _logger.LogWarning("Security incident not found: {IncidentId}", incidentId.Value);
            throw new InvalidOperationException($"Security incident {incidentId.Value} not found");
        }

        var securityEvent = securityIncident.ToSecurityEvent();
        
        // Reconstruct assessment from entity data (Domain layer doesn't know about Application DTOs)
        var affectedResourcesList = JsonSerializer.Deserialize<IEnumerable<string>>(securityIncident.AffectedResources) ?? Enumerable.Empty<string>();
        var compromisedCredentialsList = JsonSerializer.Deserialize<IEnumerable<string>>(securityIncident.CompromisedCredentials) ?? Enumerable.Empty<string>();
        var remediationActionsList = JsonSerializer.Deserialize<IEnumerable<RemediationAction>>(securityIncident.RemediationActions) ?? Enumerable.Empty<RemediationAction>();
        var assessment = SecurityIncidentAssessment.Create(
            securityIncident.Severity,
            securityIncident.ThreatType,
            affectedResourcesList,
            compromisedCredentialsList,
            securityIncident.RecommendedContainment,
            remediationActionsList);

        // Query related events for the report
        var relatedEvents = await _auditLogger.QuerySecurityEventsAsync(
            userId: securityEvent.UserId,
            eventType: securityEvent.EventType,
            startDate: securityEvent.Timestamp.AddHours(-24),
            endDate: securityEvent.Timestamp.AddHours(1),
            cancellationToken: cancellationToken);

        var report = new
        {
            IncidentId = incidentId.Value,
            CreatedAt = securityIncident.CreatedAt,
            ContainedAt = securityIncident.ContainedAt,
            ContainmentStrategy = securityIncident.ContainmentStrategy?.ToString(),
            SecurityEvent = new
            {
                EventType = securityEvent.EventType.ToString(),
                Timestamp = securityEvent.Timestamp,
                UserId = securityEvent.UserId,
                IpAddress = securityEvent.IpAddress,
                Resource = securityEvent.Resource,
                Action = securityEvent.Action,
                Succeeded = securityEvent.Succeeded,
                Details = securityEvent.Details
            },
            Assessment = new
            {
                Severity = assessment.Severity.ToString(),
                ThreatType = assessment.ThreatType.ToString(),
                AffectedResources = assessment.AffectedResources,
                CompromisedCredentials = assessment.CompromisedCredentials,
                RecommendedContainment = assessment.RecommendedContainment.ToString(),
                RemediationActions = assessment.RemediationActions.Select(ra => new
                {
                    ra.Action,
                    ra.Description,
                    ra.Priority,
                    ra.EstimatedTime
                })
            },
            RelatedEvents = relatedEvents.Select(e => new
            {
                EventType = e.EventType.ToString(),
                Timestamp = e.Timestamp,
                UserId = e.UserId,
                IpAddress = e.IpAddress,
                Resource = e.Resource,
                Action = e.Action,
                Succeeded = e.Succeeded
            }),
            Summary = new
            {
                TotalRelatedEvents = relatedEvents.Count(),
                TimeToContain = securityIncident.ContainedAt.HasValue
                    ? securityIncident.ContainedAt.Value - securityIncident.CreatedAt
                    : (TimeSpan?)null
            }
        };

        var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        _logger.LogInformation("Incident report generated. IncidentId: {IncidentId}", incidentId.Value);

        return reportJson;
    }

    public async Task RevokeCredentialsAsync(
        CredentialRevocationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger.LogInformation(
            "Revoking credentials. CredentialId: {CredentialId}, Type: {CredentialType}, Reason: {Reason}",
            request.CredentialId, request.CredentialType, request.Reason);

        try
        {
            if (string.Equals(request.CredentialType, "ApiKey", StringComparison.OrdinalIgnoreCase))
            {
                await _credentialRevocationService.RevokeApiKeyAsync(
                    request.CredentialId,
                    cancellationToken);
            }
            else if (string.Equals(request.CredentialType, "JwtToken", StringComparison.OrdinalIgnoreCase))
            {
                await _credentialRevocationService.RevokeJwtTokenAsync(
                    request.CredentialId,
                    cancellationToken);
            }
            else
            {
                // For other credential types, try API key first, then JWT token
                try
                {
                    await _credentialRevocationService.RevokeApiKeyAsync(
                        request.CredentialId,
                        cancellationToken);
                }
                catch
                {
                    await _credentialRevocationService.RevokeJwtTokenAsync(
                        request.CredentialId,
                        cancellationToken);
                }
            }

            _logger.LogInformation("Credentials revoked successfully. CredentialId: {CredentialId}", request.CredentialId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke credentials. CredentialId: {CredentialId}", request.CredentialId);
            throw;
        }
    }

    private SecurityIncidentSeverity DetermineSeverity(
        SecurityEvent securityEvent,
        IEnumerable<SecurityEvent> relatedEvents)
    {
        var relatedEventsList = relatedEvents.ToList();
        var failureCount = relatedEventsList.Count(e => !e.Succeeded);

        // Critical: Data breach, credential compromise, or > 50 failed attempts
        if (securityEvent.EventType == SecurityEventType.DataBreach ||
            securityEvent.EventType == SecurityEventType.CredentialCompromise ||
            failureCount > 50)
        {
            return SecurityIncidentSeverity.Critical;
        }

        // High: Unauthorized access, malicious payload, or > 20 failed attempts
        if (securityEvent.EventType == SecurityEventType.UnauthorizedAccess ||
            securityEvent.EventType == SecurityEventType.MaliciousPayload ||
            failureCount > 20)
        {
            return SecurityIncidentSeverity.High;
        }

        // Medium: Suspicious patterns or > 10 failed attempts
        if (securityEvent.EventType == SecurityEventType.SuspiciousPaymentPattern ||
            securityEvent.EventType == SecurityEventType.SuspiciousAuthentication ||
            failureCount > 10)
        {
            return SecurityIncidentSeverity.Medium;
        }

        // Low: Rate limit exceeded or authentication failures
        return SecurityIncidentSeverity.Low;
    }

    private SecurityThreatType DetermineThreatType(SecurityEvent securityEvent)
    {
        return securityEvent.EventType switch
        {
            SecurityEventType.AuthenticationFailure => SecurityThreatType.CredentialAttack,
            SecurityEventType.SuspiciousAuthentication => SecurityThreatType.CredentialAttack,
            SecurityEventType.UnauthorizedAccess => SecurityThreatType.UnauthorizedAccess,
            SecurityEventType.SuspiciousPaymentPattern => SecurityThreatType.PaymentFraud,
            SecurityEventType.CredentialCompromise => SecurityThreatType.CredentialAttack,
            SecurityEventType.DataBreach => SecurityThreatType.DataExfiltration,
            SecurityEventType.MaliciousPayload => SecurityThreatType.Malware,
            SecurityEventType.DDoS => SecurityThreatType.DenialOfService,
            SecurityEventType.RateLimitExceeded => SecurityThreatType.DenialOfService,
            _ => SecurityThreatType.Unknown
        };
    }

    private IEnumerable<string> IdentifyAffectedResources(
        SecurityEvent securityEvent,
        IEnumerable<SecurityEvent> relatedEvents)
    {
        var resources = new HashSet<string> { securityEvent.Resource };

        foreach (var relatedEvent in relatedEvents)
        {
            resources.Add(relatedEvent.Resource);
        }

        return resources;
    }

    private IEnumerable<string> IdentifyCompromisedCredentials(
        SecurityEvent securityEvent,
        IEnumerable<SecurityEvent> relatedEvents)
    {
        var credentials = new HashSet<string>();

        // Extract credential IDs from event details if present
        if (!string.IsNullOrWhiteSpace(securityEvent.Details))
        {
            // In production, parse structured details to extract credential IDs
            if (securityEvent.Details.Contains("apiKey", StringComparison.OrdinalIgnoreCase) ||
                securityEvent.Details.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                // Extract credential ID from details (simplified - would need proper parsing)
                if (securityEvent.UserId != null)
                {
                    credentials.Add(securityEvent.UserId);
                }
            }
        }

        return credentials;
    }

    private ContainmentStrategy DetermineContainmentStrategy(
        SecurityEvent securityEvent,
        SecurityIncidentSeverity severity,
        SecurityThreatType threatType)
    {
        // Critical incidents require immediate containment
        if (severity == SecurityIncidentSeverity.Critical)
        {
            if (threatType == SecurityThreatType.CredentialAttack)
            {
                return ContainmentStrategy.RevokeCredentials;
            }
            if (threatType == SecurityThreatType.DenialOfService)
            {
                return ContainmentStrategy.BlockIpAddress;
            }
            return ContainmentStrategy.IsolatePod;
        }

        // High severity incidents
        if (severity == SecurityIncidentSeverity.High)
        {
            if (securityEvent.IpAddress != null)
            {
                return ContainmentStrategy.BlockIpAddress;
            }
            return ContainmentStrategy.DisableFeature;
        }

        // Medium/Low severity incidents
        return ContainmentStrategy.DisableFeature;
    }

    private IEnumerable<RemediationAction> GenerateRemediationActions(
        SecurityEvent securityEvent,
        SecurityIncidentSeverity severity,
        SecurityThreatType threatType,
        IEnumerable<string> compromisedCredentials)
    {
        var actions = new List<RemediationAction>();

        // Revoke compromised credentials
        foreach (var credential in compromisedCredentials)
        {
            actions.Add(RemediationAction.RevokeCredentials(credential));
        }

        // Block IP address if available
        if (securityEvent.IpAddress != null && severity >= SecurityIncidentSeverity.Medium)
        {
            actions.Add(RemediationAction.BlockIpAddress(securityEvent.IpAddress));
        }

        // Notify security team
        actions.Add(RemediationAction.NotifySecurityTeam(
            $"Security incident detected: {securityEvent.EventType} on {securityEvent.Resource}"));

        // Additional actions based on threat type
        if (threatType == SecurityThreatType.DenialOfService)
        {
            actions.Add(RemediationAction.BlockIpAddress(securityEvent.IpAddress ?? "Unknown"));
        }

        return actions;
    }

    private async Task ExecuteContainmentStrategyAsync(
        SecurityEvent securityEvent,
        ContainmentStrategy strategy,
        CancellationToken cancellationToken)
    {
        switch (strategy)
        {
            case ContainmentStrategy.RevokeCredentials:
                if (!string.IsNullOrWhiteSpace(securityEvent.UserId))
                {
                    // Try to revoke as API key first, then as JWT token
                    try
                    {
                        await _credentialRevocationService.RevokeApiKeyAsync(
                            securityEvent.UserId,
                            cancellationToken);
                    }
                    catch
                    {
                        await _credentialRevocationService.RevokeJwtTokenAsync(
                            securityEvent.UserId,
                            cancellationToken);
                    }
                }
                break;

            case ContainmentStrategy.BlockIpAddress:
                // In production, integrate with firewall/network security service
                _logger.LogWarning(
                    "IP blocking not implemented. IP: {IpAddress} should be blocked",
                    securityEvent.IpAddress);
                break;

            case ContainmentStrategy.IsolatePod:
                // In production, integrate with Kubernetes API
                _logger.LogWarning("Pod isolation not implemented. Pod should be isolated");
                break;

            case ContainmentStrategy.DisableFeature:
                // In production, integrate with feature flag service
                _logger.LogWarning("Feature disabling not implemented. Feature should be disabled");
                break;

            case ContainmentStrategy.ScaleDown:
                // In production, integrate with Kubernetes HPA
                _logger.LogWarning("Scale down not implemented. Service should be scaled down");
                break;

            case ContainmentStrategy.NetworkIsolation:
                // In production, integrate with network security service
                _logger.LogWarning("Network isolation not implemented. Network should be isolated");
                break;
        }
    }
}

