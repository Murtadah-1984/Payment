using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

public class PaymentMethodTests
{
    [Theory]
    [InlineData("CreditCard")]
    [InlineData("DebitCard")]
    [InlineData("PayPal")]
    [InlineData("BankTransfer")]
    [InlineData("Crypto")]
    [InlineData("TapToPay")]
    [InlineData("Wallet")]
    [InlineData("Card")]
    [InlineData("Cash")]
    public void FromString_ShouldCreatePaymentMethod_WhenValid(string value)
    {
        // Act
        var method = PaymentMethod.FromString(value);

        // Assert
        method.Value.Should().Be(value);
    }

    [Fact]
    public void FromString_ShouldBeCaseInsensitive()
    {
        // Act
        var method = PaymentMethod.FromString("creditcard");

        // Assert
        method.Value.Should().Be("creditcard");
    }

    [Fact]
    public void FromString_ShouldThrowException_WhenInvalid()
    {
        // Act & Assert
        var act = () => PaymentMethod.FromString("InvalidMethod");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid payment method: InvalidMethod*")
            .And.ParamName.Should().Be("value");
    }

    [Fact]
    public void CreditCard_ShouldReturnCreditCardMethod()
    {
        // Act
        var method = PaymentMethod.CreditCard;

        // Assert
        method.Value.Should().Be("CreditCard");
    }

    [Fact]
    public void DebitCard_ShouldReturnDebitCardMethod()
    {
        // Act
        var method = PaymentMethod.DebitCard;

        // Assert
        method.Value.Should().Be("DebitCard");
    }

    [Fact]
    public void TapToPay_ShouldReturnTapToPayMethod()
    {
        // Act
        var method = PaymentMethod.TapToPay;

        // Assert
        method.Value.Should().Be("TapToPay");
    }
}

