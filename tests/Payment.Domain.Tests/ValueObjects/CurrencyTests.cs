using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

public class CurrencyTests
{
    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("CAD")]
    [InlineData("AUD")]
    [InlineData("CHF")]
    [InlineData("CNY")]
    [InlineData("INR")]
    [InlineData("BRL")]
    public void FromCode_ShouldCreateCurrency_WhenCodeIsSupported(string code)
    {
        // Act
        var currency = Currency.FromCode(code);

        // Assert
        currency.Code.Should().Be(code.ToUpperInvariant());
    }

    [Fact]
    public void FromCode_ShouldCreateCurrency_WhenCodeIsLowerCase()
    {
        // Act
        var currency = Currency.FromCode("usd");

        // Assert
        currency.Code.Should().Be("USD");
    }

    [Fact]
    public void FromCode_ShouldThrowException_WhenCodeIsNull()
    {
        // Act & Assert
        var act = () => Currency.FromCode(null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Currency code cannot be null or empty*")
            .And.ParamName.Should().Be("code");
    }

    [Fact]
    public void FromCode_ShouldThrowException_WhenCodeIsEmpty()
    {
        // Act & Assert
        var act = () => Currency.FromCode(string.Empty);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Currency code cannot be null or empty*")
            .And.ParamName.Should().Be("code");
    }

    [Fact]
    public void FromCode_ShouldThrowException_WhenCodeIsWhitespace()
    {
        // Act & Assert
        var act = () => Currency.FromCode("   ");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Currency code cannot be null or empty*")
            .And.ParamName.Should().Be("code");
    }

    [Fact]
    public void FromCode_ShouldThrowException_WhenCodeIsUnsupported()
    {
        // Act & Assert
        var act = () => Currency.FromCode("XYZ");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Unsupported currency code: XYZ*")
            .And.ParamName.Should().Be("code");
    }

    [Fact]
    public void USD_ShouldReturnUSDCurrency()
    {
        // Act
        var currency = Currency.USD;

        // Assert
        currency.Code.Should().Be("USD");
    }

    [Fact]
    public void EUR_ShouldReturnEURCurrency()
    {
        // Act
        var currency = Currency.EUR;

        // Assert
        currency.Code.Should().Be("EUR");
    }

    [Fact]
    public void GBP_ShouldReturnGBPCurrency()
    {
        // Act
        var currency = Currency.GBP;

        // Assert
        currency.Code.Should().Be("GBP");
    }
}

