using FluentAssertions;
using Moq;
using Microsoft.FeatureManagement;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Handlers;
using Payment.Application.Services;
using Xunit;

namespace Payment.Application.Tests.Handlers;

/// <summary>
/// Tests for feature flags usage in CreatePaymentCommandHandler (Feature Flags #17).
/// </summary>
public class FeatureFlagsTests
{
    private readonly Mock<IPaymentOrchestrator> _orchestratorMock;
    private readonly Mock<IFeatureManager> _featureManagerMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<CreatePaymentCommandHandler>> _loggerMock;
    private readonly CreatePaymentCommandHandler _handler;

    public FeatureFlagsTests()
    {
        _orchestratorMock = new Mock<IPaymentOrchestrator>();
        _featureManagerMock = new Mock<IFeatureManager>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<CreatePaymentCommandHandler>>();
        
        _handler = new CreatePaymentCommandHandler(
            _orchestratorMock.Object,
            _featureManagerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCheckFraudDetection_WhenFeatureIsEnabled()
    {
        // Arrange
        var command = CreateValidCommand();
        _featureManagerMock.Setup(f => f.IsEnabledAsync("FraudDetection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        _orchestratorMock.Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateValidPaymentDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _featureManagerMock.Verify(f => f.IsEnabledAsync("FraudDetection", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCheckFraudDetection_WhenFeatureIsDisabled()
    {
        // Arrange
        var command = CreateValidCommand();
        _featureManagerMock.Setup(f => f.IsEnabledAsync("FraudDetection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        _orchestratorMock.Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateValidPaymentDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _featureManagerMock.Verify(f => f.IsEnabledAsync("FraudDetection", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldProcessPayment_RegardlessOfFeatureFlag()
    {
        // Arrange
        var command = CreateValidCommand();
        var expectedDto = CreateValidPaymentDto();
        
        _featureManagerMock.Setup(f => f.IsEnabledAsync("FraudDetection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        _orchestratorMock.Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(100.50m);
        _orchestratorMock.Verify(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CreatePaymentCommand CreateValidCommand()
    {
        return new CreatePaymentCommand(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123");
    }

    private static PaymentDto CreateValidPaymentDto()
    {
        return new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);
    }
}

