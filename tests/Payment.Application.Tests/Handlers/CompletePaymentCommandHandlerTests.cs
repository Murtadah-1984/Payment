using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Payment.Application.Commands;
using Payment.Application.Handlers;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;
using Payment.Domain.Tests.Helpers;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Handlers;

public class CompletePaymentCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly IPaymentStateService _stateService;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly CompletePaymentCommandHandler _handler;

    public CompletePaymentCommandHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _stateService = MockPaymentStateService.CreatePermissive();
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["Settlement:Currency"]).Returns("USD");
        
        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentRepositoryMock.Object);
        _handler = new CompletePaymentCommandHandler(
            _unitOfWorkMock.Object,
            _stateService,
            null, // No settlement service for backward compatibility tests
            _configurationMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCompletePayment_WhenPaymentExists()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");
        payment.Process("txn-123", _stateService);

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CompletePaymentCommand(paymentId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(paymentId);
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        _paymentRepositoryMock.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenPaymentDoesNotExist()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);

        var command = new CompletePaymentCommand(paymentId);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Payment with ID {paymentId} not found");
    }

    [Fact]
    public async Task Handle_ShouldProcessSettlement_WhenSettlementServiceProvided()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100m),
            Currency.FromCode("EUR"),
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");
        payment.Process("txn-123", _stateService);

        var settlementServiceMock = new Mock<ISettlementService>();
        settlementServiceMock
            .Setup(s => s.ProcessSettlementAsync(payment, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handlerWithSettlement = new CompletePaymentCommandHandler(
            _unitOfWorkMock.Object,
            _stateService,
            settlementServiceMock.Object,
            _configurationMock.Object);

        var command = new CompletePaymentCommand(paymentId, "USD");

        // Act
        var result = await handlerWithSettlement.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        settlementServiceMock.Verify(
            s => s.ProcessSettlementAsync(payment, "USD", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUseConfigurationSettlementCurrency_WhenNotProvidedInCommand()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100m),
            Currency.FromCode("EUR"),
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");
        payment.Process("txn-123", _stateService);

        var settlementServiceMock = new Mock<ISettlementService>();
        settlementServiceMock
            .Setup(s => s.ProcessSettlementAsync(payment, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handlerWithSettlement = new CompletePaymentCommandHandler(
            _unitOfWorkMock.Object,
            _stateService,
            settlementServiceMock.Object,
            _configurationMock.Object);

        var command = new CompletePaymentCommand(paymentId); // No settlement currency

        // Act
        await handlerWithSettlement.Handle(command, CancellationToken.None);

        // Assert
        settlementServiceMock.Verify(
            s => s.ProcessSettlementAsync(payment, "USD", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCompletePayment_EvenWhenSettlementFails()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100m),
            Currency.FromCode("EUR"),
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");
        payment.Process("txn-123", _stateService);

        var settlementServiceMock = new Mock<ISettlementService>();
        settlementServiceMock
            .Setup(s => s.ProcessSettlementAsync(payment, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Settlement fails

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handlerWithSettlement = new CompletePaymentCommandHandler(
            _unitOfWorkMock.Object,
            _stateService,
            settlementServiceMock.Object,
            _configurationMock.Object);

        var command = new CompletePaymentCommand(paymentId);

        // Act
        var result = await handlerWithSettlement.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        payment.Status.Should().Be(PaymentStatus.Succeeded); // Payment still completes
    }
}

