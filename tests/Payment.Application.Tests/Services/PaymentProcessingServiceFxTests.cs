using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;
using PaymentResult = Payment.Domain.Interfaces.PaymentResult;

namespace Payment.Application.Tests.Services;

/// <summary>
/// Unit tests for PaymentProcessingService with FX conversion integration.
/// Tests automatic currency conversion when provider and payment currencies differ.
/// </summary>
public class PaymentProcessingServiceFxTests
{
    private readonly Mock<ILogger<PaymentProcessingService>> _loggerMock;
    private readonly Mock<IFxConversionService> _fxConversionServiceMock;
    private readonly Mock<IPaymentProvider> _providerMock;
    private readonly PaymentProcessingService _service;

    public PaymentProcessingServiceFxTests()
    {
        _loggerMock = new Mock<ILogger<PaymentProcessingService>>();
        _fxConversionServiceMock = new Mock<IFxConversionService>();
        _providerMock = new Mock<IPaymentProvider>();

        _service = new PaymentProcessingService(_loggerMock.Object, _fxConversionServiceMock.Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotConvert_WhenProviderSupportsPaymentCurrency()
    {
        // Arrange
        var payment = CreatePayment("USD", 100.00m);
        _providerMock.Setup(p => p.ProviderName).Returns("Stripe");
        
        // Stripe supports USD in catalog
        _providerMock
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "txn-123", null, null));

        // Act
        await _service.ProcessPaymentAsync(payment, _providerMock.Object);

        // Assert
        _fxConversionServiceMock.Verify(
            x => x.ConvertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldConvert_WhenProviderDoesNotSupportPaymentCurrency()
    {
        // Arrange
        var payment = CreatePayment("EUR", 100.00m);
        _providerMock.Setup(p => p.ProviderName).Returns("ZainCash");
        
        // ZainCash supports IQD and USD, not EUR
        var fxResult = new FxConversionResultDto(
            OriginalAmount: 100.00m,
            ConvertedAmount: 110.00m,
            FromCurrency: "EUR",
            ToCurrency: "USD",
            Rate: 1.10m,
            Timestamp: DateTime.UtcNow);

        _fxConversionServiceMock
            .Setup(x => x.ConvertAsync("EUR", It.IsAny<string>(), 100.00m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fxResult);

        _providerMock
            .Setup(p => p.ProcessPaymentAsync(It.Is<PaymentRequest>(r => r.Currency.Code == "USD"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "txn-123", null, null));

        // Act
        await _service.ProcessPaymentAsync(payment, _providerMock.Object);

        // Assert
        _fxConversionServiceMock.Verify(
            x => x.ConvertAsync("EUR", It.IsAny<string>(), 100.00m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldUseConvertedAmount_WhenConversionSucceeds()
    {
        // Arrange
        var payment = CreatePayment("EUR", 100.00m);
        _providerMock.Setup(p => p.ProviderName).Returns("ZainCash");
        
        var fxResult = new FxConversionResultDto(
            OriginalAmount: 100.00m,
            ConvertedAmount: 110.00m,
            FromCurrency: "EUR",
            ToCurrency: "USD",
            Rate: 1.10m,
            Timestamp: DateTime.UtcNow);

        _fxConversionServiceMock
            .Setup(x => x.ConvertAsync("EUR", It.IsAny<string>(), 100.00m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fxResult);

        PaymentRequest? capturedRequest = null;
        _providerMock
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRequest request, CancellationToken ct) =>
            {
                capturedRequest = request;
                return new PaymentResult(true, "txn-123", null, null);
            });

        // Act
        await _service.ProcessPaymentAsync(payment, _providerMock.Object);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Amount.Value.Should().Be(110.00m);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldContinueWithOriginalCurrency_WhenConversionFails()
    {
        // Arrange
        var payment = CreatePayment("EUR", 100.00m);
        _providerMock.Setup(p => p.ProviderName).Returns("ZainCash");
        
        _fxConversionServiceMock
            .Setup(x => x.ConvertAsync("EUR", "IQD", 100.00m, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FX API error"));

        PaymentRequest? capturedRequest = null;
        _providerMock
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRequest request, CancellationToken ct) =>
            {
                capturedRequest = request;
                return new PaymentResult(true, "txn-123", null, null);
            });

        // Act
        await _service.ProcessPaymentAsync(payment, _providerMock.Object);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Currency.Code.Should().Be("EUR");
        capturedRequest.Amount.Value.Should().Be(100.00m);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotConvert_WhenFxServiceIsNull()
    {
        // Arrange
        var serviceWithoutFx = new PaymentProcessingService(_loggerMock.Object, null);
        var payment = CreatePayment("EUR", 100.00m);
        _providerMock.Setup(p => p.ProviderName).Returns("ZainCash");

        _providerMock
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "txn-123", null, null));

        // Act
        await serviceWithoutFx.ProcessPaymentAsync(payment, _providerMock.Object);

        // Assert
        _fxConversionServiceMock.Verify(
            x => x.ConvertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotConvert_WhenProviderNotInCatalog()
    {
        // Arrange
        var payment = CreatePayment("EUR", 100.00m);
        _providerMock.Setup(p => p.ProviderName).Returns("UnknownProvider");

        _providerMock
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "txn-123", null, null));

        // Act
        await _service.ProcessPaymentAsync(payment, _providerMock.Object);

        // Assert
        _fxConversionServiceMock.Verify(
            x => x.ConvertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotConvert_WhenProviderHasNoSupportedCurrencies()
    {
        // Arrange
        var payment = CreatePayment("EUR", 100.00m);
        _providerMock.Setup(p => p.ProviderName).Returns("ProviderWithNoCurrencies");

        _providerMock
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "txn-123", null, null));

        // Act
        await _service.ProcessPaymentAsync(payment, _providerMock.Object);

        // Assert
        _fxConversionServiceMock.Verify(
            x => x.ConvertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldLogConversion_WhenConversionSucceeds()
    {
        // Arrange
        var payment = CreatePayment("EUR", 100.00m);
        _providerMock.Setup(p => p.ProviderName).Returns("ZainCash");
        
        var fxResult = new FxConversionResultDto(
            OriginalAmount: 100.00m,
            ConvertedAmount: 110.00m,
            FromCurrency: "EUR",
            ToCurrency: "USD",
            Rate: 1.10m,
            Timestamp: DateTime.UtcNow);

        _fxConversionServiceMock
            .Setup(x => x.ConvertAsync("EUR", It.IsAny<string>(), 100.00m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fxResult);

        _providerMock
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "txn-123", null, null));

        // Act
        await _service.ProcessPaymentAsync(payment, _providerMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("FX conversion completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldLogError_WhenConversionFails()
    {
        // Arrange
        var payment = CreatePayment("EUR", 100.00m);
        _providerMock.Setup(p => p.ProviderName).Returns("ZainCash");
        
        _fxConversionServiceMock
            .Setup(x => x.ConvertAsync("EUR", "IQD", 100.00m, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FX API error"));

        _providerMock
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "txn-123", null, null));

        // Act
        await _service.ProcessPaymentAsync(payment, _providerMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to convert currency")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static PaymentEntity CreatePayment(string currency, decimal amount)
    {
        return new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(amount),
            Currency.FromCode(currency),
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-456");
    }
}

