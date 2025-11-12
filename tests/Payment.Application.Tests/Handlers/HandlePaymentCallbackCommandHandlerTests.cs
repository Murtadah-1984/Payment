using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Application.Commands;
using Payment.Application.Handlers;
using Payment.Application.Services;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Handlers;

public class HandlePaymentCallbackCommandHandlerTests
{
    private readonly Mock<IPaymentProviderFactory> _providerFactoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<ILogger<HandlePaymentCallbackCommandHandler>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly HandlePaymentCallbackCommandHandler _handler;

    public HandlePaymentCallbackCommandHandlerTests()
    {
        _providerFactoryMock = new Mock<IPaymentProviderFactory>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _loggerMock = new Mock<ILogger<HandlePaymentCallbackCommandHandler>>();
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["Settlement:Currency"]).Returns("USD");
        
        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentRepositoryMock.Object);
        
        var stateService = Payment.Domain.Tests.Helpers.MockPaymentStateService.CreatePermissive();
        _handler = new HandlePaymentCallbackCommandHandler(
            _providerFactoryMock.Object,
            _unitOfWorkMock.Object,
            stateService,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldProcessCallback_WhenProviderSupportsCallbacks()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var transactionId = "txn-123";
        var orderId = "order-456";
        var callbackData = new Dictionary<string, string> { { "token", "test-token" } };

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            orderId);

        var mockProvider = new Mock<IPaymentProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("ZainCash");

        var mockCallbackProvider = mockProvider.As<IPaymentCallbackProvider>();
        mockCallbackProvider.Setup(p => p.VerifyCallbackAsync(
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(
                Success: true,
                TransactionId: transactionId,
                FailureReason: null,
                ProviderMetadata: new Dictionary<string, string> { { "OrderId", orderId } }));

        _providerFactoryMock.Setup(f => f.Create("ZainCash"))
            .Returns(mockProvider.Object);

        _paymentRepositoryMock.Setup(r => r.GetByTransactionIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new HandlePaymentCallbackCommand("ZainCash", callbackData);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(paymentId);
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.TransactionId.Should().Be(transactionId);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenProviderDoesNotSupportCallbacks()
    {
        // Arrange
        var callbackData = new Dictionary<string, string> { { "token", "test-token" } };

        var mockProvider = new Mock<IPaymentProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("Stripe");

        _providerFactoryMock.Setup(f => f.Create("Stripe"))
            .Returns(mockProvider.Object);

        var command = new HandlePaymentCallbackCommand("Stripe", callbackData);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenCallbackVerificationFails()
    {
        // Arrange
        var callbackData = new Dictionary<string, string> { { "token", "invalid-token" } };

        var mockProvider = new Mock<IPaymentProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("ZainCash");

        var mockCallbackProvider = mockProvider.As<IPaymentCallbackProvider>();
        mockCallbackProvider.Setup(p => p.VerifyCallbackAsync(
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: "Invalid token",
                ProviderMetadata: null));

        _providerFactoryMock.Setup(f => f.Create("ZainCash"))
            .Returns(mockProvider.Object);

        var command = new HandlePaymentCallbackCommand("ZainCash", callbackData);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenPaymentNotFound()
    {
        // Arrange
        var transactionId = "txn-123";
        var callbackData = new Dictionary<string, string> { { "token", "test-token" } };

        var mockProvider = new Mock<IPaymentProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("ZainCash");

        var mockCallbackProvider = mockProvider.As<IPaymentCallbackProvider>();
        mockCallbackProvider.Setup(p => p.VerifyCallbackAsync(
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(
                Success: true,
                TransactionId: transactionId,
                FailureReason: null,
                ProviderMetadata: null));

        _providerFactoryMock.Setup(f => f.Create("ZainCash"))
            .Returns(mockProvider.Object);

        _paymentRepositoryMock.Setup(r => r.GetByTransactionIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);
        _paymentRepositoryMock.Setup(r => r.GetByOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);

        var command = new HandlePaymentCallbackCommand("ZainCash", callbackData);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldFindPaymentByOrderId_WhenTransactionIdNotFound()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var transactionId = "txn-123";
        var orderId = "order-456";
        var callbackData = new Dictionary<string, string> { { "token", "test-token" } };

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            orderId);

        var mockProvider = new Mock<IPaymentProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("ZainCash");

        var mockCallbackProvider = mockProvider.As<IPaymentCallbackProvider>();
        mockCallbackProvider.Setup(p => p.VerifyCallbackAsync(
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(
                Success: true,
                TransactionId: transactionId,
                FailureReason: null,
                ProviderMetadata: new Dictionary<string, string> { { "OrderId", orderId } }));

        _providerFactoryMock.Setup(f => f.Create("ZainCash"))
            .Returns(mockProvider.Object);

        _paymentRepositoryMock.Setup(r => r.GetByTransactionIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);
        _paymentRepositoryMock.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new HandlePaymentCallbackCommand("ZainCash", callbackData);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(paymentId);
    }

    [Fact]
    public async Task Handle_ShouldProcessSettlement_WhenPaymentCompletesViaCallback()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var transactionId = "txn-123";
        var orderId = "order-456";
        var callbackData = new Dictionary<string, string> { { "token", "test-token" } };

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100m),
            Currency.FromCode("EUR"),
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            orderId);

        var mockProvider = new Mock<IPaymentProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("ZainCash");

        var mockCallbackProvider = mockProvider.As<IPaymentCallbackProvider>();
        mockCallbackProvider.Setup(p => p.VerifyCallbackAsync(
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(
                Success: true,
                TransactionId: transactionId,
                FailureReason: null,
                ProviderMetadata: new Dictionary<string, string> { { "OrderId", orderId } }));

        _providerFactoryMock.Setup(f => f.Create("ZainCash"))
            .Returns(mockProvider.Object);

        _paymentRepositoryMock.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var settlementServiceMock = new Mock<ISettlementService>();
        settlementServiceMock
            .Setup(s => s.ProcessSettlementAsync(payment, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var stateService = Payment.Domain.Tests.Helpers.MockPaymentStateService.CreatePermissive();
        var handlerWithSettlement = new HandlePaymentCallbackCommandHandler(
            _providerFactoryMock.Object,
            _unitOfWorkMock.Object,
            stateService,
            _configurationMock.Object,
            _loggerMock.Object,
            null,
            settlementServiceMock.Object);

        var command = new HandlePaymentCallbackCommand("ZainCash", callbackData);

        // Act
        var result = await handlerWithSettlement.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(paymentId);
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        settlementServiceMock.Verify(
            s => s.ProcessSettlementAsync(payment, "USD", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

