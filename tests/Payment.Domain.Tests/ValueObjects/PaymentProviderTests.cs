using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

public class PaymentProviderTests
{
    [Theory]
    [InlineData("ZainCash")]
    [InlineData("AsiaHawala")]
    [InlineData("Stripe")]
    [InlineData("FIB")]
    [InlineData("Square")]
    [InlineData("Helcim")]
    [InlineData("AmazonPaymentServices")]
    [InlineData("Telr")]
    [InlineData("Checkout")]
    [InlineData("Verifone")]
    [InlineData("Paytabs")]
    [InlineData("Tap")]
    [InlineData("TapToPay")]
    public void FromString_ShouldCreatePaymentProvider_WhenValid(string name)
    {
        // Act
        var provider = PaymentProvider.FromString(name);

        // Assert
        provider.Name.Should().Be(name);
    }

    [Fact]
    public void FromString_ShouldBeCaseInsensitive()
    {
        // Act
        var provider = PaymentProvider.FromString("zaincash");

        // Assert
        provider.Name.Should().Be("zaincash");
    }

    [Fact]
    public void FromString_ShouldThrowException_WhenInvalid()
    {
        // Act & Assert
        var act = () => PaymentProvider.FromString("InvalidProvider");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid payment provider: InvalidProvider*")
            .And.ParamName.Should().Be("name");
    }

    [Fact]
    public void ZainCash_ShouldReturnZainCashProvider()
    {
        // Act
        var provider = PaymentProvider.ZainCash;

        // Assert
        provider.Name.Should().Be("ZainCash");
    }

    [Fact]
    public void Helcim_ShouldReturnHelcimProvider()
    {
        // Act
        var provider = PaymentProvider.Helcim;

        // Assert
        provider.Name.Should().Be("Helcim");
    }

    [Fact]
    public void TapToPay_ShouldReturnTapToPayProvider()
    {
        // Act
        var provider = PaymentProvider.TapToPay;

        // Assert
        provider.Name.Should().Be("TapToPay");
    }
}

