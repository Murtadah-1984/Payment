using FluentAssertions;
using Moq;
using Microsoft.FeatureManagement;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Handlers;
using Payment.Application.Services;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Handlers;

public class CreatePaymentCommandHandlerTests
{
    private readonly Mock<IPaymentOrchestrator> _orchestratorMock;
    private readonly Mock<IFeatureManager> _featureManagerMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<CreatePaymentCommandHandler>> _loggerMock;
    private readonly CreatePaymentCommandHandler _handler;

    public CreatePaymentCommandHandlerTests()
    {
        _orchestratorMock = new Mock<IPaymentOrchestrator>();
        _featureManagerMock = new Mock<IFeatureManager>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<CreatePaymentCommandHandler>>();
        _handler = new CreatePaymentCommandHandler(
            _orchestratorMock.Object,
            _featureManagerMock.Object,
            _loggerMock.Object);
        
        _featureManagerMock.Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task Handle_ShouldCreatePayment_WhenValidCommand()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var command = new CreatePaymentCommand(
            requestId,
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
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _orchestratorMock.Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(100.50m);
        result.Currency.Should().Be("USD");
        result.PaymentMethod.Should().Be("CreditCard");
        result.MerchantId.Should().Be("merchant-123");
        result.OrderId.Should().Be("order-456");
        result.Status.Should().Be("Pending");

        _orchestratorMock.Verify(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnExistingPayment_WhenOrderIdExists()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var command = new CreatePaymentCommand(
            requestId,
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123");

        var existingDto = new PaymentDto(
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

        _orchestratorMock.Setup(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be("order-456");
        _orchestratorMock.Verify(o => o.ProcessPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

