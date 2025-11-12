using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Controllers.Admin;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Enums;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.API.Tests.Controllers.Admin;

/// <summary>
/// Integration tests for SecurityIncidentController admin endpoints.
/// Tests security incident management endpoints.
/// </summary>
public class SecurityIncidentControllerTests
{
    private readonly Mock<ISecurityIncidentResponseService> _securityIncidentResponseServiceMock;
    private readonly Mock<ILogger<SecurityIncidentController>> _loggerMock;
    private readonly SecurityIncidentController _controller;

    public SecurityIncidentControllerTests()
    {
        _securityIncidentResponseServiceMock = new Mock<ISecurityIncidentResponseService>();
        _loggerMock = new Mock<ILogger<SecurityIncidentController>>();

        _controller = new SecurityIncidentController(
            _securityIncidentResponseServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AssessIncident_ShouldReturnOk_WithAssessment()
    {
        // Arrange
        var securityEvent = SecurityEvent.Create(
            SecurityEventType.UnauthorizedAccess,
            DateTime.UtcNow,
            userId: "user123",
            ipAddress: "192.168.1.1",
            resource: "/api/admin/payments",
            action: "UnauthorizedAccess",
            succeeded: false,
            details: "Unauthorized access attempt");

        var expectedAssessment = new SecurityIncidentAssessment(
            SecurityIncidentSeverity.High,
            SecurityThreatType.UnauthorizedAccess,
            new[] { "/api/admin/payments" },
            Enumerable.Empty<string>(),
            ContainmentStrategy.BlockIpAddress,
            new[]
            {
                RemediationAction.BlockIpAddress("192.168.1.1"),
                RemediationAction.NotifySecurityTeam("Security incident detected")
            });

        _securityIncidentResponseServiceMock
            .Setup(s => s.AssessIncidentAsync(
                It.IsAny<SecurityEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssessment);

        // Act
        var result = await _controller.AssessIncident(securityEvent, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedAssessment);
    }

    [Fact]
    public async Task ContainIncident_ShouldReturnNoContent_WhenSuccessful()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        var request = new ContainmentRequest(
            Strategy: ContainmentStrategy.BlockIpAddress,
            Reason: "Security threat detected");

        _securityIncidentResponseServiceMock
            .Setup(s => s.ContainIncidentAsync(
                It.IsAny<SecurityIncidentId>(),
                It.IsAny<ContainmentStrategy>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ContainIncident(incidentId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        _securityIncidentResponseServiceMock.Verify(
            s => s.ContainIncidentAsync(
                It.Is<SecurityIncidentId>(id => id.Value == incidentId),
                ContainmentStrategy.BlockIpAddress,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ContainIncident_ShouldReturnNotFound_WhenIncidentNotFound()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        var request = new ContainmentRequest(
            Strategy: ContainmentStrategy.BlockIpAddress,
            Reason: "Security threat detected");

        _securityIncidentResponseServiceMock
            .Setup(s => s.ContainIncidentAsync(
                It.IsAny<SecurityIncidentId>(),
                It.IsAny<ContainmentStrategy>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Security incident not found"));

        // Act
        var result = await _controller.ContainIncident(incidentId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetIncidentReport_ShouldReturnOk_WithJsonReport()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        var expectedReport = "{\"IncidentId\":\"" + incidentId + "\",\"Status\":\"Contained\"}";

        _securityIncidentResponseServiceMock
            .Setup(s => s.GenerateIncidentReportAsync(
                It.IsAny<SecurityIncidentId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _controller.GetIncidentReport(incidentId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedReport);
    }

    [Fact]
    public async Task GetIncidentReport_ShouldReturnNotFound_WhenIncidentNotFound()
    {
        // Arrange
        var incidentId = Guid.NewGuid();

        _securityIncidentResponseServiceMock
            .Setup(s => s.GenerateIncidentReportAsync(
                It.IsAny<SecurityIncidentId>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Security incident not found"));

        // Act
        var result = await _controller.GetIncidentReport(incidentId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RevokeCredentials_ShouldReturnNoContent_WhenSuccessful()
    {
        // Arrange
        var request = CredentialRevocationRequest.Create(
            credentialId: "api-key-123",
            credentialType: "ApiKey",
            reason: "Security incident",
            revokedBy: "admin");

        _securityIncidentResponseServiceMock
            .Setup(s => s.RevokeCredentialsAsync(
                It.IsAny<CredentialRevocationRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RevokeCredentials(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        _securityIncidentResponseServiceMock.Verify(
            s => s.RevokeCredentialsAsync(
                request,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AssessIncident_ShouldReturnBadRequest_WhenServiceThrowsException()
    {
        // Arrange
        var securityEvent = SecurityEvent.Create(
            SecurityEventType.AuthenticationFailure,
            DateTime.UtcNow,
            userId: "user123",
            ipAddress: "192.168.1.1",
            resource: "/api/payments",
            action: "AuthenticationFailure",
            succeeded: false,
            details: "Failed authentication");

        _securityIncidentResponseServiceMock
            .Setup(s => s.AssessIncidentAsync(
                It.IsAny<SecurityEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.AssessIncident(securityEvent, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}

