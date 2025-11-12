using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.Services;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Services;

public class SettlementServiceTests
{
    private readonly Mock<IExchangeRateService> _exchangeRateServiceMock;
    private readonly Mock<ILogger<SettlementService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly SettlementService _settlementService;

    public SettlementServiceTests()
    {
        _exchangeRateServiceMock = new Mock<IExchangeRateService>();
        _loggerMock = new Mock<ILogger<SettlementService>>();
        _configurationMock = new Mock<IConfiguration>();

        var configurationSectionMock = new Mock<IConfigurationSection>();
        configurationSectionMock.Setup(s => s.Value).Returns("USD");
        _configurationMock.Setup(c => c["Settlement:Currency"]).Returns("USD");

        _settlementService = new SettlementService(
            _exchangeRateServiceMock.Object,
            _loggerMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public async Task ProcessSettlementAsync_ShouldConvertCurrency_WhenCurrenciesDiffer()
    {
        // Arrange
        var payment = CreateTestPayment(amount: 100m, currency: "EUR");
        var exchangeRate = 1.08m;
        var settlementAmount = 108m;

        _exchangeRateServiceMock
            .Setup(s => s.GetExchangeRateAsync("EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeRate);

        _exchangeRateServiceMock
            .Setup(s => s.ConvertAsync(100m, "EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlementAmount);

        // Act
        var result = await _settlementService.ProcessSettlementAsync(payment, "USD", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        payment.SettlementCurrency.Should().NotBeNull();
        payment.SettlementCurrency!.Code.Should().Be("USD");
        payment.SettlementAmount.Should().Be(settlementAmount);
        payment.ExchangeRate.Should().Be(exchangeRate);
        payment.SettledAt.Should().NotBeNull();
        payment.SettledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _exchangeRateServiceMock.Verify(
            s => s.GetExchangeRateAsync("EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _exchangeRateServiceMock.Verify(
            s => s.ConvertAsync(100m, "EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessSettlementAsync_ShouldNotConvert_WhenCurrenciesMatch()
    {
        // Arrange
        var payment = CreateTestPayment(amount: 100m, currency: "USD");

        // Act
        var result = await _settlementService.ProcessSettlementAsync(payment, "USD", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        payment.SettlementCurrency.Should().BeNull();
        payment.SettlementAmount.Should().BeNull();
        payment.ExchangeRate.Should().BeNull();
        payment.SettledAt.Should().BeNull();

        _exchangeRateServiceMock.Verify(
            s => s.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _exchangeRateServiceMock.Verify(
            s => s.ConvertAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessSettlementAsync_ShouldUseDefaultSettlementCurrency_WhenNotProvided()
    {
        // Arrange
        var payment = CreateTestPayment(amount: 100m, currency: "EUR");
        var exchangeRate = 1.08m;
        var settlementAmount = 108m;

        _exchangeRateServiceMock
            .Setup(s => s.GetExchangeRateAsync("EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeRate);

        _exchangeRateServiceMock
            .Setup(s => s.ConvertAsync(100m, "EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlementAmount);

        // Act
        var result = await _settlementService.ProcessSettlementAsync(payment, null!, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        payment.SettlementCurrency!.Code.Should().Be("USD");
    }

    [Fact]
    public async Task ProcessSettlementAsync_ShouldHandleExchangeRateServiceException_Gracefully()
    {
        // Arrange
        var payment = CreateTestPayment(amount: 100m, currency: "EUR");

        _exchangeRateServiceMock
            .Setup(s => s.GetExchangeRateAsync("EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Exchange rate service unavailable"));

        // Act
        var result = await _settlementService.ProcessSettlementAsync(payment, "USD", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        payment.SettlementCurrency.Should().BeNull();
        payment.SettlementAmount.Should().BeNull();
    }

    [Fact]
    public async Task ProcessSettlementAsync_ShouldHandleConversionException_Gracefully()
    {
        // Arrange
        var payment = CreateTestPayment(amount: 100m, currency: "EUR");
        var exchangeRate = 1.08m;

        _exchangeRateServiceMock
            .Setup(s => s.GetExchangeRateAsync("EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeRate);

        _exchangeRateServiceMock
            .Setup(s => s.ConvertAsync(100m, "EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Conversion failed"));

        // Act
        var result = await _settlementService.ProcessSettlementAsync(payment, "USD", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        payment.SettlementCurrency.Should().BeNull();
    }

    [Fact]
    public async Task ProcessSettlementAsync_ShouldUsePaymentUpdatedAt_ForExchangeRateDate()
    {
        // Arrange
        var payment = CreateTestPayment(amount: 100m, currency: "EUR");
        var paymentDate = DateTime.UtcNow.AddDays(-1);
        payment.GetType().GetProperty("UpdatedAt")!.SetValue(payment, paymentDate);

        var exchangeRate = 1.08m;
        var settlementAmount = 108m;

        DateTime? capturedDate = null;
        _exchangeRateServiceMock
            .Setup(s => s.GetExchangeRateAsync("EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateTime?, CancellationToken>((from, to, date, ct) => capturedDate = date)
            .ReturnsAsync(exchangeRate);

        _exchangeRateServiceMock
            .Setup(s => s.ConvertAsync(100m, "EUR", "USD", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlementAmount);

        // Act
        await _settlementService.ProcessSettlementAsync(payment, "USD", CancellationToken.None);

        // Assert
        capturedDate.Should().BeCloseTo(paymentDate, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("GBP", "USD", "1.27", "127")]
    [InlineData("JPY", "USD", "0.0067", "0.67")]
    [InlineData("EUR", "GBP", "0.85", "85")]
    public async Task ProcessSettlementAsync_ShouldHandleMultipleCurrencies(
        string fromCurrency,
        string toCurrency,
        string exchangeRateStr,
        string expectedSettlementAmountStr)
    {
        // Arrange
        var exchangeRate = decimal.Parse(exchangeRateStr);
        var expectedSettlementAmount = decimal.Parse(expectedSettlementAmountStr);
        var payment = CreateTestPayment(amount: 100m, currency: fromCurrency);

        _exchangeRateServiceMock
            .Setup(s => s.GetExchangeRateAsync(fromCurrency, toCurrency, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeRate);

        _exchangeRateServiceMock
            .Setup(s => s.ConvertAsync(100m, fromCurrency, toCurrency, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSettlementAmount);

        // Act
        var result = await _settlementService.ProcessSettlementAsync(payment, toCurrency, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        payment.SettlementCurrency!.Code.Should().Be(toCurrency);
        payment.SettlementAmount.Should().Be(expectedSettlementAmount);
        payment.ExchangeRate.Should().Be(exchangeRate);
    }

    [Fact]
    public async Task ProcessSettlementAsync_ShouldThrowArgumentNullException_WhenPaymentIsNull()
    {
        // Act & Assert
        var act = async () => await _settlementService.ProcessSettlementAsync(null!, "USD", CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("payment");
    }

    private static PaymentEntity CreateTestPayment(decimal amount = 100m, string currency = "USD")
    {
        return new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(amount),
            Currency.FromCode(currency),
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");
    }
}

