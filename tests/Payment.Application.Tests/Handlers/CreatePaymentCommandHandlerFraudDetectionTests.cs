using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Microsoft.FeatureManagement;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Handlers;
using Payment.Application.Services;
using Payment.Domain.Exceptions;
using Payment.Domain.Interfaces;
using Xunit;

namespace Payment.Application.Tests.Handlers;

/// <summary>
/// Tests for CreatePaymentCommandHandler with fraud detection integration (Fraud Detection #22).
/// </summary>
public class CreatePaymentCommandHandlerFraudDetectionTests
{
    private readonly Mock<IPaymentOrchestrator> _orchestratorMock;
    private readonly Mock<IFeatureManager> _featureManagerMock;
    private readonly Mock<IFraudDetectionService> _fraudDetectionServiceMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<CreatePaymentCommandHandler>> _loggerMock;
    private readonly CreatePaymentCommandHandler _handler;
    private readonly Microsoft.AspNetCore.Http.DefaultHttpContext _httpContext;

    public CreatePaymentCommandHandlerFraudDetectionTests()
    {
        _orchestratorMock = new Mock<IPaymentOrchestrator>();
        _featureManagerMock = new Mock<IFeatureManager>();
        _fraudDetectionServiceMock = new Mock<IFraudDetectionService>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<CreatePaymentCommandHandler>>();
        _httpContext = new DefaultHttpContext();

        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(_httpContext);
        _httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        _handler = new CreatePaymentCommandHandler(
            _orchestratorMock.Object,
            _featureManagerMock.Object,
            _loggerMock.Object,
            _httpContextAccessorMock.Object,
            _fraudDetectionServiceMock.Object);

        // Default: fraud detection feature enabled
        _featureManagerMock
            .Setup(f => f.IsEnabledAsync("FraudDetection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task Handle_ShouldProceed_WhenFraudDetectionReturnsLowRisk()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123");

        var fraudResult = FraudCheckResult.LowRisk("txn-123");
        _fraudDetectionServiceMock
            .Setup(f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fraudResult);

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null, null, null, null, null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _orchestratorMock
            .Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _fraudDetectionServiceMock.Verify(
            f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _orchestratorMock.Verify(
            o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldProceed_WhenFraudDetectionReturnsMediumRisk()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            1000.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123");

        var fraudResult = FraudCheckResult.MediumRisk(
            0.65m,
            new[] { "Unusual amount", "New device" },
            "txn-456");

        _fraudDetectionServiceMock
            .Setup(f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fraudResult);

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            1000.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null, null, null, null, null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _orchestratorMock
            .Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _fraudDetectionServiceMock.Verify(
            f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _orchestratorMock.Verify(
            o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowFraudDetectionException_WhenFraudDetectionReturnsHighRisk()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            5000.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123");

        var fraudResult = FraudCheckResult.HighRisk(
            0.95m,
            new[] { "Suspicious IP", "Velocity check failed" },
            "txn-789");

        _fraudDetectionServiceMock
            .Setup(f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fraudResult);

        // Act & Assert
        var action = async () => await _handler.Handle(command, CancellationToken.None);
        await action.Should().ThrowAsync<FraudDetectionException>()
            .WithMessage("*fraud risk*");

        _fraudDetectionServiceMock.Verify(
            f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _orchestratorMock.Verify(
            o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotCallFraudDetection_WhenFeatureFlagIsDisabled()
    {
        // Arrange
        _featureManagerMock
            .Setup(f => f.IsEnabledAsync("FraudDetection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123");

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null, null, null, null, null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _orchestratorMock
            .Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _fraudDetectionServiceMock.Verify(
            f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _orchestratorMock.Verify(
            o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotCallFraudDetection_WhenServiceIsNull()
    {
        // Arrange
        var handlerWithoutFraudService = new CreatePaymentCommandHandler(
            _orchestratorMock.Object,
            _featureManagerMock.Object,
            _loggerMock.Object,
            _httpContextAccessorMock.Object,
            null); // No fraud detection service

        _featureManagerMock
            .Setup(f => f.IsEnabledAsync("FraudDetection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123");

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null, null, null, null, null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _orchestratorMock
            .Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await handlerWithoutFraudService.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _orchestratorMock.Verify(
            o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldExtractIpAddress_FromHttpContext()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123");

        var fraudResult = FraudCheckResult.LowRisk();
        FraudCheckRequest? capturedRequest = null;

        _fraudDetectionServiceMock
            .Setup(f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FraudCheckRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(fraudResult);

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null, null, null, null, null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _orchestratorMock
            .Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.IpAddress.Should().Be("192.168.1.1");
    }

    [Fact]
    public async Task Handle_ShouldExtractIpAddress_FromXForwardedForHeader()
    {
        // Arrange
        _httpContext.Connection.RemoteIpAddress = null;
        _httpContext.Request.Headers["X-Forwarded-For"] = "10.0.0.1";

        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123");

        var fraudResult = FraudCheckResult.LowRisk();
        FraudCheckRequest? capturedRequest = null;

        _fraudDetectionServiceMock
            .Setup(f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FraudCheckRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(fraudResult);

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null, null, null, null, null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _orchestratorMock
            .Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.IpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public async Task Handle_ShouldPassAllRequestData_ToFraudDetectionService()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123",
            SystemFeePercent: 5.0m,
            CustomerEmail: "customer@example.com",
            CustomerPhone: "+1234567890",
            CustomerId: "customer-123",
            DeviceId: "device-456");

        var fraudResult = FraudCheckResult.LowRisk();
        FraudCheckRequest? capturedRequest = null;

        _fraudDetectionServiceMock
            .Setup(f => f.CheckAsync(It.IsAny<FraudCheckRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FraudCheckRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(fraudResult);

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null, null, null, null, null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _orchestratorMock
            .Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Amount.Should().Be(100.50m);
        capturedRequest.Currency.Should().Be("USD");
        capturedRequest.PaymentMethod.Should().Be("CreditCard");
        capturedRequest.MerchantId.Should().Be("merchant-123");
        capturedRequest.OrderId.Should().Be("order-456");
        capturedRequest.CustomerEmail.Should().Be("customer@example.com");
        capturedRequest.CustomerPhone.Should().Be("+1234567890");
        capturedRequest.CustomerId.Should().Be("customer-123");
        capturedRequest.DeviceId.Should().Be("device-456");
        capturedRequest.ProjectCode.Should().Be("PROJECT-001");
    }
}

