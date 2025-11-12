using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.Handlers;
using Payment.Application.Queries;
using Payment.Domain.Common;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Handlers;

/// <summary>
/// Unit tests for GetPaymentByIdQueryHandler with Result pattern (Result Pattern #16).
/// </summary>
public class GetPaymentByIdQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<ILogger<GetPaymentByIdQueryHandler>> _loggerMock;
    private readonly GetPaymentByIdQueryHandler _handler;

    public GetPaymentByIdQueryHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _cacheMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<GetPaymentByIdQueryHandler>>();
        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentRepositoryMock.Object);
        _handler = new GetPaymentByIdQueryHandler(_unitOfWorkMock.Object, _cacheMock.Object, _loggerMock.Object);
        
        // Setup cache to return null by default (cache miss)
        _cacheMock.Setup(c => c.GetAsync<Payment.Application.DTOs.PaymentDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment.Application.DTOs.PaymentDto?)null);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenPaymentExists()
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

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<Payment.Application.DTOs.PaymentDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var query = new GetPaymentByIdQuery(paymentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(paymentId);
        result.Value.Amount.Should().Be(100.50m);
        result.Value.Currency.Should().Be("USD");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnFailureResult_WhenPaymentDoesNotExist()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);

        var query = new GetPaymentByIdQuery(paymentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(ErrorCodes.PaymentNotFound);
        result.Error.Message.Should().Contain(paymentId.ToString());
    }

    [Fact]
    public async Task Handle_ShouldReturnCachedPayment_WhenCacheHit()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var cachedDto = new Payment.Application.DTOs.PaymentDto(
            paymentId,
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

        _cacheMock.Setup(c => c.GetAsync<Payment.Application.DTOs.PaymentDto>(It.Is<string>(k => k.Contains(paymentId.ToString())), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        var query = new GetPaymentByIdQuery(paymentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(paymentId);
        _paymentRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailureResult_WhenExceptionOccurs()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var query = new GetPaymentByIdQuery(paymentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(ErrorCodes.InternalError);
        result.Error.Message.Should().Contain("error occurred");
    }
}

