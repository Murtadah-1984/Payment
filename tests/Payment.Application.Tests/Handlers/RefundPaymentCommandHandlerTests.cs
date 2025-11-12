using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.Commands;
using Payment.Application.Handlers;
using Payment.Domain.Common;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;
using Payment.Domain.Tests.Helpers;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Handlers;

/// <summary>
/// Unit tests for RefundPaymentCommandHandler with Result pattern (Result Pattern #16).
/// </summary>
public class RefundPaymentCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<ILogger<RefundPaymentCommandHandler>> _loggerMock;
    private readonly IPaymentStateService _stateService;
    private readonly RefundPaymentCommandHandler _handler;

    public RefundPaymentCommandHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _loggerMock = new Mock<ILogger<RefundPaymentCommandHandler>>();
        _stateService = MockPaymentStateService.CreatePermissive();
        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentRepositoryMock.Object);
        _handler = new RefundPaymentCommandHandler(_unitOfWorkMock.Object, _stateService, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenPaymentExistsAndCompleted()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var refundTransactionId = "refund-txn-123";
        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");
        payment.Process("txn-123", _stateService);
        payment.Complete(_stateService);

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new RefundPaymentCommand(paymentId, refundTransactionId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(paymentId);
        result.Error.Should().BeNull();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.TransactionId.Should().Be(refundTransactionId);
        _paymentRepositoryMock.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailureResult_WhenPaymentDoesNotExist()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);

        var command = new RefundPaymentCommand(paymentId, "refund-txn-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(ErrorCodes.PaymentNotFound);
        result.Error.Message.Should().Contain(paymentId.ToString());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenPaymentAlreadyRefunded()
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
        payment.Complete(_stateService);
        payment.Refund("refund-txn-123", _stateService);

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var command = new RefundPaymentCommand(paymentId, "refund-txn-456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be("Refunded");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailureResult_WhenExceptionOccurs()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var command = new RefundPaymentCommand(paymentId, "refund-txn-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(ErrorCodes.InternalError);
    }
}

