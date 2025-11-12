using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Controllers.Admin;
using Payment.Application.DTOs;
using Payment.Application.Interfaces;
using Payment.Application.Services;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.API.Tests.Controllers.Admin;

/// <summary>
/// Integration tests for IncidentController admin endpoints.
/// Tests payment failure incident management endpoints.
/// </summary>
public class IncidentControllerTests
{
    private readonly Mock<IIncidentResponseService> _incidentResponseServiceMock;
    private readonly Mock<IIncidentReportGenerator> _reportGeneratorMock;
    private readonly Mock<ILogger<IncidentController>> _loggerMock;
    private readonly IncidentController _controller;

    public IncidentControllerTests()
    {
        _incidentResponseServiceMock = new Mock<IIncidentResponseService>();
        _reportGeneratorMock = new Mock<IIncidentReportGenerator>();
        _loggerMock = new Mock<ILogger<IncidentController>>();

        _controller = new IncidentController(
            _incidentResponseServiceMock.Object,
            _reportGeneratorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AssessPaymentFailure_ShouldReturnOk_WithAssessment()
    {
        // Arrange
        var request = new AssessPaymentFailureRequest(
            Provider: "Stripe",
            FailureType: "ProviderError",
            StartTime: DateTime.UtcNow.AddHours(-1),
            EndTime: DateTime.UtcNow,
            Metadata: new Dictionary<string, object> { { "error", "timeout" } });

        var expectedAssessment = IncidentAssessment.Create(
            severity: Domain.Enums.IncidentSeverity.High,
            rootCause: "Payment provider returned an error response",
            affectedProviders: new[] { "Stripe" },
            affectedPaymentCount: 50,
            estimatedResolutionTime: TimeSpan.FromMinutes(15),
            recommendedActions: new[]
            {
                new RecommendedAction("ContactProvider", "Contact payment provider support", "Medium", "30 minutes")
            });

        _incidentResponseServiceMock
            .Setup(s => s.AssessPaymentFailureAsync(
                It.IsAny<PaymentFailureContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssessment);

        // Act
        var result = await _controller.AssessPaymentFailure(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedAssessment);
    }

    [Fact]
    public async Task ProcessRefunds_ShouldReturnOk_WithRefundResult()
    {
        // Arrange
        var paymentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var request = new ProcessRefundsRequest(
            PaymentIds: paymentIds,
            Reason: "Payment failure incident");

        var refundStatuses = new Dictionary<PaymentId, bool>
        {
            { PaymentId.FromGuid(paymentIds[0]), true },
            { PaymentId.FromGuid(paymentIds[1]), false }
        };

        var expectedResult = RefundResult.Create(refundStatuses, new[] { "Payment 2 failed" });

        _incidentResponseServiceMock
            .Setup(s => s.ProcessAutomaticRefundsAsync(
                It.IsAny<IEnumerable<PaymentId>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundStatuses);

        // Act
        var result = await _controller.ProcessRefunds(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var refundResult = okResult!.Value as RefundResult;
        refundResult!.TotalProcessed.Should().Be(2);
        refundResult.Successful.Should().Be(1);
        refundResult.Failed.Should().Be(1);
    }

    [Fact]
    public async Task ResetCircuitBreaker_ShouldReturnNoContent()
    {
        // Arrange
        var provider = "Stripe";

        // Act
        var result = await _controller.ResetCircuitBreaker(provider, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task GetIncidentMetrics_ShouldReturnOk_WithMetrics()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddHours(-24);
        var endDate = DateTime.UtcNow;

        var timeRange = new TimeRange(startDate, endDate);
        var expectedMetrics = IncidentMetricsDto.Create(
            totalIncidents: 10,
            criticalIncidents: 2,
            highSeverityIncidents: 3,
            mediumSeverityIncidents: 3,
            lowSeverityIncidents: 2,
            averageResolutionTime: TimeSpan.FromMinutes(20),
            incidentsByType: new Dictionary<string, int>
            {
                { "ProviderError", 5 },
                { "Timeout", 3 },
                { "NetworkError", 2 }
            });

        _incidentResponseServiceMock
            .Setup(s => s.GetIncidentMetricsAsync(
                It.IsAny<TimeRange>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _controller.GetIncidentMetrics(startDate, endDate, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedMetrics);
    }

    [Fact]
    public async Task AssessPaymentFailure_ShouldReturnBadRequest_WhenServiceThrowsException()
    {
        // Arrange
        var request = new AssessPaymentFailureRequest(
            Provider: "Stripe",
            FailureType: "ProviderError",
            StartTime: DateTime.UtcNow.AddHours(-1),
            EndTime: null,
            Metadata: null);

        _incidentResponseServiceMock
            .Setup(s => s.AssessPaymentFailureAsync(
                It.IsAny<PaymentFailureContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.AssessPaymentFailure(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}

