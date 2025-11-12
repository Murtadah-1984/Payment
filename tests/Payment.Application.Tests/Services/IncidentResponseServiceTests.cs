using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Services;

public class IncidentResponseServiceTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<ICircuitBreakerService> _circuitBreakerServiceMock;
    private readonly Mock<IRefundService> _refundServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ILogger<IncidentResponseService>> _loggerMock;
    private readonly IncidentResponseService _service;

    public IncidentResponseServiceTests()
    {
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _circuitBreakerServiceMock = new Mock<ICircuitBreakerService>();
        _refundServiceMock = new Mock<IRefundService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<IncidentResponseService>>();

        _service = new IncidentResponseService(
            _paymentRepositoryMock.Object,
            _circuitBreakerServiceMock.Object,
            _refundServiceMock.Object,
            _notificationServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AssessPaymentFailureAsync_ShouldReturnCriticalSeverity_WhenMoreThan100PaymentsAffected()
    {
        // Arrange
        var context = new PaymentFailureContext(
            StartTime: DateTime.UtcNow.AddMinutes(-10),
            EndTime: null,
            Provider: "Stripe",
            FailureType: PaymentFailureType.ProviderError,
            AffectedPaymentCount: 150,
            Metadata: new Dictionary<string, object>());

        _circuitBreakerServiceMock
            .Setup(s => s.GetProvidersWithOpenCircuitBreakersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<string>());

        // Act
        var result = await _service.AssessPaymentFailureAsync(context);

        // Assert
        result.Severity.Should().Be(IncidentSeverity.Critical);
        result.AffectedPaymentCount.Should().Be(150);
        result.RootCause.Should().NotBeNullOrEmpty();
        result.RecommendedActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AssessPaymentFailureAsync_ShouldReturnHighSeverity_When50To100PaymentsAffected()
    {
        // Arrange
        var context = new PaymentFailureContext(
            StartTime: DateTime.UtcNow.AddMinutes(-10),
            EndTime: null,
            Provider: "Stripe",
            FailureType: PaymentFailureType.Timeout,
            AffectedPaymentCount: 75,
            Metadata: new Dictionary<string, object>());

        _circuitBreakerServiceMock
            .Setup(s => s.GetProvidersWithOpenCircuitBreakersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<string>());

        // Act
        var result = await _service.AssessPaymentFailureAsync(context);

        // Assert
        result.Severity.Should().Be(IncidentSeverity.High);
    }

    [Fact]
    public async Task AssessPaymentFailureAsync_ShouldReturnCriticalSeverity_WhenProviderUnavailable()
    {
        // Arrange
        var context = new PaymentFailureContext(
            StartTime: DateTime.UtcNow.AddMinutes(-10),
            EndTime: null,
            Provider: "Stripe",
            FailureType: PaymentFailureType.ProviderUnavailable,
            AffectedPaymentCount: 5,
            Metadata: new Dictionary<string, object>());

        _circuitBreakerServiceMock
            .Setup(s => s.GetProvidersWithOpenCircuitBreakersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Stripe" });

        // Act
        var result = await _service.AssessPaymentFailureAsync(context);

        // Assert
        result.Severity.Should().Be(IncidentSeverity.Critical);
        result.AffectedProviders.Should().Contain("Stripe");
    }

    [Fact]
    public async Task AssessPaymentFailureAsync_ShouldIncludeSwitchProviderAction_WhenProviderUnavailable()
    {
        // Arrange
        var context = new PaymentFailureContext(
            StartTime: DateTime.UtcNow.AddMinutes(-10),
            EndTime: null,
            Provider: "Stripe",
            FailureType: PaymentFailureType.ProviderUnavailable,
            AffectedPaymentCount: 10,
            Metadata: new Dictionary<string, object>());

        _circuitBreakerServiceMock
            .Setup(s => s.GetProvidersWithOpenCircuitBreakersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Stripe" });

        // Act
        var result = await _service.AssessPaymentFailureAsync(context);

        // Assert
        result.RecommendedActions.Should().Contain(a => a.Action == "SwitchProvider");
    }

    [Fact]
    public async Task NotifyStakeholdersAsync_ShouldReturnTrue_WhenNotificationSucceeds()
    {
        // Arrange
        _notificationServiceMock
            .Setup(s => s.NotifyStakeholdersAsync(
                It.IsAny<IncidentSeverity>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.NotifyStakeholdersAsync(
            IncidentSeverity.High,
            "Test notification message");

        // Assert
        result.Should().BeTrue();
        _notificationServiceMock.Verify(
            s => s.NotifyStakeholdersAsync(
                IncidentSeverity.High,
                "Test notification message",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyStakeholdersAsync_ShouldReturnFalse_WhenNotificationFails()
    {
        // Arrange
        _notificationServiceMock
            .Setup(s => s.NotifyStakeholdersAsync(
                It.IsAny<IncidentSeverity>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.NotifyStakeholdersAsync(
            IncidentSeverity.Medium,
            "Test notification message");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NotifyStakeholdersAsync_ShouldThrowArgumentException_WhenMessageIsEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyStakeholdersAsync(IncidentSeverity.Low, string.Empty));
    }

    [Fact]
    public async Task ProcessAutomaticRefundsAsync_ShouldProcessAllRefunds()
    {
        // Arrange
        var paymentIds = new[]
        {
            PaymentId.NewId(),
            PaymentId.NewId(),
            PaymentId.NewId()
        };

        var refundResults = new Dictionary<PaymentId, bool>
        {
            { paymentIds[0], true },
            { paymentIds[1], true },
            { paymentIds[2], false }
        };

        _refundServiceMock
            .Setup(s => s.ProcessRefundsAsync(
                It.IsAny<IEnumerable<PaymentId>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundResults);

        // Act
        var result = await _service.ProcessAutomaticRefundsAsync(paymentIds);

        // Assert
        result.Should().HaveCount(3);
        result[paymentIds[0]].Should().BeTrue();
        result[paymentIds[1]].Should().BeTrue();
        result[paymentIds[2]].Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAutomaticRefundsAsync_ShouldReturnEmptyDictionary_WhenNoPaymentIdsProvided()
    {
        // Act
        var result = await _service.ProcessAutomaticRefundsAsync(Enumerable.Empty<PaymentId>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetIncidentMetricsAsync_ShouldReturnMetrics_ForTimeRange()
    {
        // Arrange
        var timeRange = TimeRange.LastHours(24);
        var failedPayments = CreateFailedPayments(10);

        _paymentRepositoryMock
            .Setup(r => r.GetByStatusAsync(PaymentStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedPayments);

        // Act
        var result = await _service.GetIncidentMetricsAsync(timeRange);

        // Assert
        result.Should().NotBeNull();
        result.TotalIncidents.Should().BeGreaterThan(0);
        result.IncidentsByType.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetIncidentMetricsAsync_ShouldFilterPaymentsByTimeRange()
    {
        // Arrange
        var timeRange = new TimeRange(
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow);

        var allFailedPayments = new List<PaymentEntity>
        {
            CreateFailedPayment("Stripe", DateTime.UtcNow.AddHours(-1)), // Within range
            CreateFailedPayment("Checkout", DateTime.UtcNow.AddHours(-3)), // Outside range
            CreateFailedPayment("Helcim", DateTime.UtcNow.AddMinutes(-30)) // Within range
        };

        _paymentRepositoryMock
            .Setup(r => r.GetByStatusAsync(PaymentStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allFailedPayments);

        // Act
        var result = await _service.GetIncidentMetricsAsync(timeRange);

        // Assert
        result.TotalIncidents.Should().Be(2); // Only payments within time range
    }

    [Fact]
    public async Task GetIncidentMetricsAsync_ShouldThrowArgumentNullException_WhenTimeRangeIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.GetIncidentMetricsAsync(null!));
    }

    [Fact]
    public async Task AssessPaymentFailureAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.AssessPaymentFailureAsync(null!));
    }

    private List<PaymentEntity> CreateFailedPayments(int count)
    {
        var payments = new List<PaymentEntity>();
        for (int i = 0; i < count; i++)
        {
            payments.Add(CreateFailedPayment($"Provider{i % 3}", DateTime.UtcNow.AddMinutes(-i)));
        }
        return payments;
    }

    private PaymentEntity CreateFailedPayment(string provider, DateTime createdAt)
    {
        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100m),
            Currency.USD,
            PaymentMethod.CreditCard,
            new PaymentProvider(provider),
            "merchant-123",
            $"order-{Guid.NewGuid()}");

        // Use state service to fail the payment
        var stateService = new Mock<Payment.Domain.Services.IPaymentStateService>();
        stateService.Setup(s => s.Transition(It.IsAny<PaymentStatus>(), PaymentTrigger.Fail))
            .Returns(PaymentStatus.Failed);

        payment.Fail("Provider error", stateService.Object);

        // Use reflection to set CreatedAt (since it's private)
        var createdAtProperty = typeof(PaymentEntity).GetProperty("CreatedAt");
        if (createdAtProperty != null && createdAtProperty.CanWrite)
        {
            createdAtProperty.SetValue(payment, createdAt);
        }

        return payment;
    }
}

