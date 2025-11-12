using FluentAssertions;
using Payment.Domain.Entities;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Domain.Tests.Entities;

public class PaymentSettlementTests
{
    [Fact]
    public void SetSettlement_ShouldSetSettlementFields_WhenValidParameters()
    {
        // Arrange
        var payment = CreateTestPayment();
        var settlementCurrency = Currency.FromCode("USD");
        var settlementAmount = 108m;
        var exchangeRate = 1.08m;

        // Act
        payment.SetSettlement(settlementCurrency, settlementAmount, exchangeRate);

        // Assert
        payment.SettlementCurrency.Should().Be(settlementCurrency);
        payment.SettlementAmount.Should().Be(settlementAmount);
        payment.ExchangeRate.Should().Be(exchangeRate);
        payment.SettledAt.Should().NotBeNull();
        payment.SettledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        payment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SetSettlement_ShouldThrowArgumentNullException_WhenSettlementCurrencyIsNull()
    {
        // Arrange
        var payment = CreateTestPayment();

        // Act & Assert
        var act = () => payment.SetSettlement(null!, 108m, 1.08m);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settlementCurrency");
    }

    [Fact]
    public void SetSettlement_ShouldThrowArgumentException_WhenSettlementAmountIsNegative()
    {
        // Arrange
        var payment = CreateTestPayment();
        var settlementCurrency = Currency.FromCode("USD");

        // Act & Assert
        var act = () => payment.SetSettlement(settlementCurrency, -10m, 1.08m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Settlement amount cannot be negative*")
            .WithParameterName("settlementAmount");
    }

    [Fact]
    public void SetSettlement_ShouldThrowArgumentException_WhenExchangeRateIsZero()
    {
        // Arrange
        var payment = CreateTestPayment();
        var settlementCurrency = Currency.FromCode("USD");

        // Act & Assert
        var act = () => payment.SetSettlement(settlementCurrency, 108m, 0m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Exchange rate must be positive*")
            .WithParameterName("exchangeRate");
    }

    [Fact]
    public void SetSettlement_ShouldThrowArgumentException_WhenExchangeRateIsNegative()
    {
        // Arrange
        var payment = CreateTestPayment();
        var settlementCurrency = Currency.FromCode("USD");

        // Act & Assert
        var act = () => payment.SetSettlement(settlementCurrency, 108m, -1.08m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Exchange rate must be positive*")
            .WithParameterName("exchangeRate");
    }

    [Fact]
    public void SetSettlement_ShouldAllowZeroSettlementAmount()
    {
        // Arrange
        var payment = CreateTestPayment();
        var settlementCurrency = Currency.FromCode("USD");

        // Act
        payment.SetSettlement(settlementCurrency, 0m, 1.08m);

        // Assert
        payment.SettlementAmount.Should().Be(0m);
    }

    [Fact]
    public void SetSettlement_ShouldUpdateSettledAtTimestamp()
    {
        // Arrange
        var payment = CreateTestPayment();
        var settlementCurrency = Currency.FromCode("USD");
        var beforeSettlement = DateTime.UtcNow;

        // Act
        payment.SetSettlement(settlementCurrency, 108m, 1.08m);

        // Assert
        payment.SettledAt.Should().NotBeNull();
        payment.SettledAt!.Value.Should().BeAfter(beforeSettlement);
        payment.SettledAt.Value.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void SetSettlement_ShouldUpdateUpdatedAtTimestamp()
    {
        // Arrange
        var payment = CreateTestPayment();
        var settlementCurrency = Currency.FromCode("USD");
        var originalUpdatedAt = payment.UpdatedAt;
        
        // Wait a bit to ensure timestamp difference
        Thread.Sleep(10);

        // Act
        payment.SetSettlement(settlementCurrency, 108m, 1.08m);

        // Assert
        payment.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Theory]
    [MemberData(nameof(GetCurrencyTestData))]
    public void SetSettlement_ShouldHandleVariousCurrencies(
        string currencyCode,
        decimal settlementAmount,
        decimal exchangeRate)
    {
        // Arrange
        var payment = CreateTestPayment();
        var settlementCurrency = Currency.FromCode(currencyCode);

        // Act
        payment.SetSettlement(settlementCurrency, settlementAmount, exchangeRate);

        // Assert
        payment.SettlementCurrency!.Code.Should().Be(currencyCode);
        payment.SettlementAmount.Should().Be(settlementAmount);
        payment.ExchangeRate.Should().Be(exchangeRate);
    }

    public static IEnumerable<object[]> GetCurrencyTestData()
    {
        yield return new object[] { "USD", 100m, 1.0m };
        yield return new object[] { "EUR", 92m, 0.92m };
        yield return new object[] { "GBP", 79m, 0.79m };
        yield return new object[] { "JPY", 15000m, 150m };
    }

    [Fact]
    public void SetSettlement_ShouldAllowMultipleCalls_WithDifferentValues()
    {
        // Arrange
        var payment = CreateTestPayment();
        var firstCurrency = Currency.FromCode("USD");
        var secondCurrency = Currency.FromCode("EUR");

        // Act
        payment.SetSettlement(firstCurrency, 100m, 1.0m);
        var firstSettledAt = payment.SettledAt;
        Thread.Sleep(10);
        payment.SetSettlement(secondCurrency, 92m, 0.92m);

        // Assert
        payment.SettlementCurrency!.Code.Should().Be("EUR");
        payment.SettlementAmount.Should().Be(92m);
        payment.ExchangeRate.Should().Be(0.92m);
        payment.SettledAt.Should().BeAfter(firstSettledAt!.Value);
    }

    private static PaymentEntity CreateTestPayment()
    {
        return new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100m),
            Currency.FromCode("EUR"),
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");
    }
}

