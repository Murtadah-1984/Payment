using FluentAssertions;
using Moq;
using Payment.Application.Handlers;
using Payment.Application.Queries;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Handlers;

public class GetPaymentByOrderIdQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly GetPaymentByOrderIdQueryHandler _handler;

    public GetPaymentByOrderIdQueryHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentRepositoryMock.Object);
        _handler = new GetPaymentByOrderIdQueryHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnPaymentDto_WhenPaymentExists()
    {
        // Arrange
        var orderId = "order-456";
        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            orderId);

        _paymentRepositoryMock.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var query = new GetPaymentByOrderIdQuery(orderId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.Amount.Should().Be(100.50m);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenPaymentDoesNotExist()
    {
        // Arrange
        var orderId = "order-456";
        _paymentRepositoryMock.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);

        var query = new GetPaymentByOrderIdQuery(orderId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

