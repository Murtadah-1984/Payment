using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Data;
using Payment.Infrastructure.Repositories;
using Xunit;

namespace Payment.Infrastructure.Tests.Repositories;

/// <summary>
/// Tests for SecurityIncidentRepository.
/// Follows AAA pattern (Arrange-Act-Assert).
/// </summary>
public class SecurityIncidentRepositoryTests : IDisposable
{
    private readonly PaymentDbContext _context;
    private readonly SecurityIncidentRepository _repository;

    public SecurityIncidentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PaymentDbContext(options);
        _repository = new SecurityIncidentRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ShouldAddSecurityIncident_WhenValid()
    {
        // Arrange
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

        var securityIncident = new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: incidentId,
            securityEvent: securityEvent,
            severity: SecurityIncidentSeverity.High,
            threatType: SecurityThreatType.UnauthorizedAccess,
            affectedResources: new[] { "/api/payments" },
            compromisedCredentials: new[] { "user123" },
            recommendedContainment: ContainmentStrategy.RevokeCredentials,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);

        // Act
        await _repository.AddAsync(securityIncident);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _context.SecurityIncidents.FirstOrDefaultAsync(s => s.Id == securityIncident.Id);
        result.Should().NotBeNull();
        result!.IncidentId.Should().Be(incidentId.Value);
        result.Severity.Should().Be(SecurityIncidentSeverity.High);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSecurityIncident_WhenExists()
    {
        // Arrange
        var incidentId = SecurityIncidentId.NewId();
        var securityEvent = SecurityEvent.Create(
            SecurityEventType.DataBreach,
            DateTime.UtcNow,
            userId: "user456",
            ipAddress: "10.0.0.1",
            resource: "/api/admin",
            action: "DataBreach",
            succeeded: false,
            details: "Data breach detected");

        var securityIncident = new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: incidentId,
            securityEvent: securityEvent,
            severity: SecurityIncidentSeverity.Critical,
            threatType: SecurityThreatType.DataExfiltration,
            affectedResources: new[] { "/api/admin" },
            compromisedCredentials: Array.Empty<string>(),
            recommendedContainment: ContainmentStrategy.IsolatePod,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);

        _context.SecurityIncidents.Add(securityIncident);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(securityIncident.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(securityIncident.Id);
        result.Severity.Should().Be(SecurityIncidentSeverity.Critical);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIncidentIdAsync_ShouldReturnSecurityIncident_WhenExists()
    {
        // Arrange
        var incidentId = SecurityIncidentId.NewId();
        var securityEvent = SecurityEvent.Create(
            SecurityEventType.CredentialCompromise,
            DateTime.UtcNow,
            userId: "user789",
            ipAddress: "172.16.0.1",
            resource: "/api/auth",
            action: "CredentialCompromise",
            succeeded: false,
            details: "Credential compromise detected");

        var securityIncident = new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: incidentId,
            securityEvent: securityEvent,
            severity: SecurityIncidentSeverity.High,
            threatType: SecurityThreatType.CredentialAttack,
            affectedResources: new[] { "/api/auth" },
            compromisedCredentials: new[] { "user789" },
            recommendedContainment: ContainmentStrategy.RevokeCredentials,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);

        _context.SecurityIncidents.Add(securityIncident);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIncidentIdAsync(incidentId);

        // Assert
        result.Should().NotBeNull();
        result!.IncidentId.Should().Be(incidentId.Value);
        result.Severity.Should().Be(SecurityIncidentSeverity.High);
    }

    [Fact]
    public async Task GetByIncidentIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var nonExistentIncidentId = SecurityIncidentId.NewId();

        // Act
        var result = await _repository.GetByIncidentIdAsync(nonExistentIncidentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllSecurityIncidents()
    {
        // Arrange
        var incident1 = CreateSecurityIncident(SecurityIncidentSeverity.Low);
        var incident2 = CreateSecurityIncident(SecurityIncidentSeverity.Medium);
        var incident3 = CreateSecurityIncident(SecurityIncidentSeverity.High);

        _context.SecurityIncidents.AddRange(incident1, incident2, incident3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(s => s.Id == incident1.Id);
        result.Should().Contain(s => s.Id == incident2.Id);
        result.Should().Contain(s => s.Id == incident3.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateSecurityIncident()
    {
        // Arrange
        var securityIncident = CreateSecurityIncident(SecurityIncidentSeverity.Medium);
        _context.SecurityIncidents.Add(securityIncident);
        await _context.SaveChangesAsync();

        // Act
        securityIncident.MarkAsContained(ContainmentStrategy.BlockIpAddress, DateTime.UtcNow);
        await _repository.UpdateAsync(securityIncident);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _context.SecurityIncidents.FirstOrDefaultAsync(s => s.Id == securityIncident.Id);
        result.Should().NotBeNull();
        result!.ContainedAt.Should().NotBeNull();
        result.ContainmentStrategy.Should().Be(ContainmentStrategy.BlockIpAddress);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveSecurityIncident()
    {
        // Arrange
        var securityIncident = CreateSecurityIncident(SecurityIncidentSeverity.Low);
        _context.SecurityIncidents.Add(securityIncident);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(securityIncident);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _context.SecurityIncidents.FirstOrDefaultAsync(s => s.Id == securityIncident.Id);
        result.Should().BeNull();
    }

    private SecurityIncident CreateSecurityIncident(SecurityIncidentSeverity severity)
    {
        var securityEvent = SecurityEvent.Create(
            SecurityEventType.AuthenticationFailure,
            DateTime.UtcNow,
            userId: "user123",
            ipAddress: "192.168.1.1",
            resource: "/api/payments",
            action: "AuthenticationFailure",
            succeeded: false,
            details: "Authentication failure");

        return new SecurityIncident(
            id: Guid.NewGuid(),
            incidentId: SecurityIncidentId.NewId(),
            securityEvent: securityEvent,
            severity: severity,
            threatType: SecurityThreatType.CredentialAttack,
            affectedResources: new[] { "/api/payments" },
            compromisedCredentials: Array.Empty<string>(),
            recommendedContainment: ContainmentStrategy.DisableFeature,
            remediationActionsJson: "[]",
            createdAt: DateTime.UtcNow);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

