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

public class GetPaymentsByMerchantQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly GetPaymentsByMerchantQueryHandler _handler;

    public GetPaymentsByMerchantQueryHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentRepositoryMock.Object);
        _handler = new GetPaymentsByMerchantQueryHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnPaymentDtos_WhenPaymentsExist()
    {
        // Arrange
        var merchantId = "merchant-123";
        var payments = new[]
        {
            new PaymentEntity(
                PaymentId.NewId(),
                Amount.FromDecimal(100.50m),
                Currency.USD,
                PaymentMethod.CreditCard,
                PaymentProvider.ZainCash,
                merchantId,
                "order-456"),
            new PaymentEntity(
                PaymentId.NewId(),
                Amount.FromDecimal(200.75m),
                Currency.EUR,
                PaymentMethod.DebitCard,
                PaymentProvider.Helcim,
                merchantId,
                "order-789")
        };

        _paymentRepositoryMock.Setup(r => r.GetByMerchantIdAsync(merchantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payments);

        var query = new GetPaymentsByMerchantQuery(merchantId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(p => p.MerchantId == merchantId).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoPaymentsExist()
    {
        // Arrange
        var merchantId = "merchant-123";
        _paymentRepositoryMock.Setup(r => r.GetByMerchantIdAsync(merchantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PaymentEntity>());

        var query = new GetPaymentsByMerchantQuery(merchantId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}

