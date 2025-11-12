using FluentAssertions;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.Entities;

/// <summary>
/// Tests for SecurityIncident entity.
/// Follows AAA pattern (Arrange-Act-Assert).
/// </summary>
public class SecurityIncidentTests
{
    [Fact]
    public void Constructor_ShouldCreateSecurityIncident_WhenValidParameters()
    {
        // Arrange
        var id = Guid.NewGuid();
        var incidentId = SecurityIncidentId.NewId();
        var securityEvent = SecurityEvent.Create(
            SecurityEventType.UnauthorizedAccess,
            DateTime.UtcNow,
            userId: "user123",
            ipAddress: "192.168.1.1",
            resource: "/api/payments",
            action: "UnauthorizedAccess",
            succeeded: false,
            details: "Unauthorized access attempt");

        // Act
        var securityIncident = new SecurityIncident(
            id: id,
            incidentId: incidentId,
            securityEvent: securityEvent,
            severity: SecurityIncidentSeverity.High,
            threatType: SecurityThreatType.UnauthorizedAccess,
            affectedResources: new[] { "/api/payments", "/api/admin" },
            compromisedCredentials: new[] { "user123" },
            recommendedContainment: ContainmentStrategy.RevokeCredentials,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);

        // Assert
        securityIncident.Id.Should().Be(id);
        securityIncident.IncidentId.Should().Be(incidentId.Value);
        securityIncident.Severity.Should().Be(SecurityIncidentSeverity.High);
        securityIncident.ThreatType.Should().Be(SecurityThreatType.UnauthorizedAccess);
        securityIncident.RecommendedContainment.Should().Be(ContainmentStrategy.RevokeCredentials);
        securityIncident.ContainedAt.Should().BeNull();
        securityIncident.ContainmentStrategy.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenIncidentIdIsNull()
    {
        // Arrange
        var securityEvent = SecurityEvent.Create(
            SecurityEventType.UnauthorizedAccess,
            DateTime.UtcNow,
            userId: "user123",
            ipAddress: "192.168.1.1",
            resource: "/api/payments",
            action: "UnauthorizedAccess",
            succeeded: false);

        // Act & Assert
        var act = () => new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: null!,
            securityEvent: securityEvent,
            severity: SecurityIncidentSeverity.High,
            threatType: SecurityThreatType.UnauthorizedAccess,
            affectedResources: Array.Empty<string>(),
            compromisedCredentials: Array.Empty<string>(),
            recommendedContainment: ContainmentStrategy.DisableFeature,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("incidentId");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSecurityEventIsNull()
    {
        // Arrange
        var incidentId = SecurityIncidentId.NewId();

        // Act & Assert
        var act = () => new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: incidentId,
            securityEvent: null!,
            severity: SecurityIncidentSeverity.High,
            threatType: SecurityThreatType.UnauthorizedAccess,
            affectedResources: Array.Empty<string>(),
            compromisedCredentials: Array.Empty<string>(),
            recommendedContainment: ContainmentStrategy.DisableFeature,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("securityEvent");
    }

    [Fact]
    public void MarkAsContained_ShouldSetContainedAtAndStrategy()
    {
        // Arrange
        var securityIncident = CreateSecurityIncident();
        var strategy = ContainmentStrategy.BlockIpAddress;
        var containedAt = DateTime.UtcNow;

        // Act
        securityIncident.MarkAsContained(strategy, containedAt);

        // Assert
        securityIncident.ContainedAt.Should().Be(containedAt);
        securityIncident.ContainmentStrategy.Should().Be(strategy);
    }

    [Fact]
    public void ToSecurityEvent_ShouldReturnCorrectSecurityEvent()
    {
        // Arrange
        var originalEvent = SecurityEvent.Create(
            SecurityEventType.DataBreach,
            DateTime.UtcNow.AddHours(-1),
            userId: "user456",
            ipAddress: "10.0.0.1",
            resource: "/api/admin",
            action: "DataBreach",
            succeeded: false,
            details: "Data breach detected");

        var securityIncident = new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: SecurityIncidentId.NewId(),
            securityEvent: originalEvent,
            severity: SecurityIncidentSeverity.Critical,
            threatType: SecurityThreatType.DataExfiltration,
            affectedResources: Array.Empty<string>(),
            compromisedCredentials: Array.Empty<string>(),
            recommendedContainment: ContainmentStrategy.IsolatePod,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);

        // Act
        var result = securityIncident.ToSecurityEvent();

        // Assert
        result.EventType.Should().Be(SecurityEventType.DataBreach);
        result.UserId.Should().Be("user456");
        result.IpAddress.Should().Be("10.0.0.1");
        result.Resource.Should().Be("/api/admin");
        result.Action.Should().Be("DataBreach");
        result.Succeeded.Should().BeFalse();
        result.Details.Should().Be("Data breach detected");
    }

    [Fact]
    public void Constructor_ShouldSerializeAffectedResources_AsJson()
    {
        // Arrange
        var affectedResources = new[] { "/api/payments", "/api/admin", "/api/users" };
        var securityEvent = CreateSecurityEvent();

        // Act
        var securityIncident = new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: SecurityIncidentId.NewId(),
            securityEvent: securityEvent,
            severity: SecurityIncidentSeverity.Medium,
            threatType: SecurityThreatType.UnauthorizedAccess,
            affectedResources: affectedResources,
            compromisedCredentials: Array.Empty<string>(),
            recommendedContainment: ContainmentStrategy.DisableFeature,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);

        // Assert
        securityIncident.AffectedResources.Should().NotBeNullOrEmpty();
        securityIncident.AffectedResources.Should().Contain("/api/payments");
        securityIncident.AffectedResources.Should().Contain("/api/admin");
    }

    [Fact]
    public void Constructor_ShouldHandleNullRemediationActionsJson()
    {
        // Arrange
        var securityEvent = CreateSecurityEvent();

        // Act
        var securityIncident = new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: SecurityIncidentId.NewId(),
            securityEvent: securityEvent,
            severity: SecurityIncidentSeverity.Low,
            threatType: SecurityThreatType.Unknown,
            affectedResources: Array.Empty<string>(),
            compromisedCredentials: Array.Empty<string>(),
            recommendedContainment: ContainmentStrategy.DisableFeature,
            remediationActionsJson: null!,
            createdAt: DateTime.UtcNow);

        // Assert
        securityIncident.RemediationActions.Should().Be("[]");
    }

    private SecurityIncident CreateSecurityIncident()
    {
        var securityEvent = CreateSecurityEvent();
        return new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: SecurityIncidentId.NewId(),
            securityEvent: securityEvent,
            severity: SecurityIncidentSeverity.Medium,
            threatType: SecurityThreatType.CredentialAttack,
            affectedResources: new[] { "/api/payments" },
            compromisedCredentials: Array.Empty<string>(),
            recommendedContainment: ContainmentStrategy.DisableFeature,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);
    }

    private SecurityEvent CreateSecurityEvent()
    {
        return SecurityEvent.Create(
            SecurityEventType.AuthenticationFailure,
            DateTime.UtcNow,
            userId: "user123",
            ipAddress: "192.168.1.1",
            resource: "/api/payments",
            action: "AuthenticationFailure",
            succeeded: false,
            details: "Authentication failure");
    }
}

