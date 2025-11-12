using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Payment.Application.DTOs;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Monitoring;
using Payment.Infrastructure.Monitoring.Channels;
using Xunit;

namespace Payment.Infrastructure.Tests.Monitoring;

/// <summary>
/// Unit tests for AlertingService.
/// Tests cover alert sending, deduplication, channel routing, and metrics integration.
/// </summary>
public class AlertingServiceTests
{
    private readonly Mock<IEnumerable<IAlertChannel>> _channelsMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<AlertingService>> _loggerMock;
    private readonly Mock<IMetricsRecorder> _metricsRecorderMock;
    private readonly AlertRulesConfiguration _alertRules;
    private readonly AlertingService _alertingService;
    private readonly List<IAlertChannel> _channels;

    public AlertingServiceTests()
    {
        _channels = new List<IAlertChannel>();
        _channelsMock = new Mock<IEnumerable<IAlertChannel>>();
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<AlertingService>>();
        _metricsRecorderMock = new Mock<IMetricsRecorder>();

        _alertRules = new AlertRulesConfiguration
        {
            PaymentFailure = new PaymentFailureAlertRules
            {
                Critical = new AlertRule
                {
                    Threshold = "> 10 failures in 5 minutes",
                    Channels = new List<string> { "PagerDuty", "Email", "Slack" }
                },
                High = new AlertRule
                {
                    Threshold = "> 5 failures in 5 minutes",
                    Channels = new List<string> { "Email", "Slack" }
                }
            },
            SecurityIncident = new SecurityIncidentAlertRules
            {
                Critical = new AlertRule
                {
                    Threshold = "Any unauthorized access",
                    Channels = new List<string> { "PagerDuty", "Email", "SMS" }
                }
            }
        };

        var options = Options.Create(_alertRules);
        _alertingService = new AlertingService(
            _channels,
            options,
            _cacheMock.Object,
            _loggerMock.Object,
            _metricsRecorderMock.Object);
    }

    [Fact]
    public async Task SendAlertAsync_ShouldSendAlert_WhenValidInput()
    {
        // Arrange
        var emailChannel = CreateMockChannel("Email", AlertSeverity.Low);
        var slackChannel = CreateMockChannel("Slack", AlertSeverity.Medium);
        _channels.Add(emailChannel.Object);
        _channels.Add(slackChannel.Object);

        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _cacheMock.Setup(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _alertingService.SendAlertAsync(
            AlertSeverity.High,
            "Test Alert",
            "Test message",
            null,
            CancellationToken.None);

        // Assert
        emailChannel.Verify(c => c.SendAsync(
            AlertSeverity.High,
            "Test Alert",
            "Test message",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        slackChannel.Verify(c => c.SendAsync(
            AlertSeverity.High,
            "Test Alert",
            "Test message",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _metricsRecorderMock.Verify(m => m.RecordAlertSent(
            "High",
            It.IsAny<string>(),
            "Generic"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendAlertAsync_ShouldDeduplicateAlert_WhenSameAlertSentTwice()
    {
        // Arrange
        var emailChannel = CreateMockChannel("Email", AlertSeverity.Low);
        _channels.Add(emailChannel.Object);

        _cacheMock.SetupSequence(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null) // First call - not duplicate
            .ReturnsAsync("cached") // Second call - duplicate
            .ReturnsAsync("cached");

        _cacheMock.Setup(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _alertingService.SendAlertAsync(
            AlertSeverity.Medium,
            "Duplicate Alert",
            "Test message",
            null,
            CancellationToken.None);

        // Second call should be deduplicated
        await _alertingService.SendAlertAsync(
            AlertSeverity.Medium,
            "Duplicate Alert",
            "Test message",
            null,
            CancellationToken.None);

        // Assert
        emailChannel.Verify(c => c.SendAsync(
            It.IsAny<AlertSeverity>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()), Times.Once); // Only called once
        _metricsRecorderMock.Verify(m => m.RecordAlertDeduplicated(
            "Medium",
            "Generic"), Times.Once);
    }

    [Fact]
    public async Task SendAlertAsync_ShouldThrowException_WhenTitleIsEmpty()
    {
        // Arrange & Act
        var act = async () => await _alertingService.SendAlertAsync(
            AlertSeverity.Low,
            string.Empty,
            "Message",
            null,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendPaymentFailureAlertAsync_ShouldSendAlert_WhenValidContext()
    {
        // Arrange
        var emailChannel = CreateMockChannel("Email", AlertSeverity.Low);
        _channels.Add(emailChannel.Object);

        var context = new PaymentFailureContext(
            StartTime: DateTime.UtcNow,
            EndTime: null,
            Provider: "Stripe",
            FailureType: PaymentFailureType.ProviderError,
            AffectedPaymentCount: 15,
            Metadata: new Dictionary<string, object>());

        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _cacheMock.Setup(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _alertingService.SendPaymentFailureAlertAsync(context, CancellationToken.None);

        // Assert
        emailChannel.Verify(c => c.SendAsync(
            It.IsAny<AlertSeverity>(),
            It.Is<string>(t => t.Contains("Payment Failure Alert")),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _metricsRecorderMock.Verify(m => m.RecordAlertSent(
            It.IsAny<string>(),
            "Email",
            "PaymentFailure"), Times.Once);
    }

    [Fact]
    public async Task SendSecurityIncidentAlertAsync_ShouldSendAlert_WhenValidEvent()
    {
        // Arrange
        var pagerDutyChannel = CreateMockChannel("PagerDuty", AlertSeverity.Critical);
        _channels.Add(pagerDutyChannel.Object);

        var securityEvent = SecurityEvent.Create(
            SecurityEventType.UnauthorizedAccess,
            DateTime.UtcNow,
            "user123",
            "192.168.1.1",
            "/api/payments",
            "GET",
            false,
            "Unauthorized access attempt");

        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _cacheMock.Setup(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _alertingService.SendSecurityIncidentAlertAsync(securityEvent, CancellationToken.None);

        // Assert
        pagerDutyChannel.Verify(c => c.SendAsync(
            It.IsAny<AlertSeverity>(),
            It.Is<string>(t => t.Contains("Security Incident")),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _metricsRecorderMock.Verify(m => m.RecordAlertSent(
            It.IsAny<string>(),
            "PagerDuty",
            "SecurityIncident"), Times.Once);
    }

    [Fact]
    public async Task SendAlertToChannelAsync_ShouldRecordMetrics_WhenChannelSucceeds()
    {
        // Arrange
        var emailChannel = CreateMockChannel("Email", AlertSeverity.Low);
        _channels.Add(emailChannel.Object);

        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _cacheMock.Setup(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _alertingService.SendAlertAsync(
            AlertSeverity.Low,
            "Test",
            "Message",
            null,
            CancellationToken.None);

        // Assert
        _metricsRecorderMock.Verify(m => m.RecordAlertSendingDuration(
            "Email",
            "Low",
            It.Is<double>(d => d >= 0)), Times.Once);
    }

    [Fact]
    public async Task SendAlertToChannelAsync_ShouldRecordFailureMetrics_WhenChannelFails()
    {
        // Arrange
        var emailChannel = CreateMockChannel("Email", AlertSeverity.Low);
        emailChannel.Setup(c => c.SendAsync(
            It.IsAny<AlertSeverity>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Channel failure"));
        _channels.Add(emailChannel.Object);

        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _cacheMock.Setup(c => c.SetStringAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _alertingService.SendAlertAsync(
            AlertSeverity.Low,
            "Test",
            "Message",
            null,
            CancellationToken.None);

        // Assert
        _metricsRecorderMock.Verify(m => m.RecordAlertChannelFailure(
            "Email",
            "Low"), Times.Once);
    }

    private Mock<IAlertChannel> CreateMockChannel(string name, AlertSeverity minimumSeverity)
    {
        var channel = new Mock<IAlertChannel>();
        channel.Setup(c => c.Name).Returns(name);
        channel.Setup(c => c.MinimumSeverity).Returns(minimumSeverity);
        channel.Setup(c => c.SendAsync(
            It.IsAny<AlertSeverity>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return channel;
    }
}

