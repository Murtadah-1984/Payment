using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

public class CardTokenTests
{
    [Fact]
    public void Constructor_ShouldCreateCardToken_WhenAllParametersAreValid()
    {
        // Act
        var cardToken = new CardToken("token_1234567890", "1234", "Visa");

        // Assert
        cardToken.Token.Should().Be("token_1234567890");
        cardToken.Last4Digits.Should().Be("1234");
        cardToken.CardBrand.Should().Be("Visa");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenTokenIsNull()
    {
        // Act & Assert
        var act = () => new CardToken(null!, "1234", "Visa");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Card token cannot be empty*")
            .And.ParamName.Should().Be("token");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenTokenIsEmpty()
    {
        // Act & Assert
        var act = () => new CardToken(string.Empty, "1234", "Visa");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Card token cannot be empty*")
            .And.ParamName.Should().Be("token");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenTokenIsWhitespace()
    {
        // Act & Assert
        var act = () => new CardToken("   ", "1234", "Visa");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Card token cannot be empty*")
            .And.ParamName.Should().Be("token");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenLast4DigitsIsNull()
    {
        // Act & Assert
        var act = () => new CardToken("token_123", null!, "Visa");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Last 4 digits cannot be empty*")
            .And.ParamName.Should().Be("last4Digits");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenLast4DigitsIsEmpty()
    {
        // Act & Assert
        var act = () => new CardToken("token_123", string.Empty, "Visa");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Last 4 digits cannot be empty*")
            .And.ParamName.Should().Be("last4Digits");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenLast4DigitsIsNot4Digits()
    {
        // Act & Assert
        var act = () => new CardToken("token_123", "123", "Visa");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Last 4 digits must be exactly 4 digits*")
            .And.ParamName.Should().Be("last4Digits");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenLast4DigitsContainsNonDigits()
    {
        // Act & Assert
        var act = () => new CardToken("token_123", "12ab", "Visa");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Last 4 digits must be exactly 4 digits*")
            .And.ParamName.Should().Be("last4Digits");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenCardBrandIsNull()
    {
        // Act & Assert
        var act = () => new CardToken("token_123", "1234", null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Card brand cannot be empty*")
            .And.ParamName.Should().Be("cardBrand");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenCardBrandIsEmpty()
    {
        // Act & Assert
        var act = () => new CardToken("token_123", "1234", string.Empty);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Card brand cannot be empty*")
            .And.ParamName.Should().Be("cardBrand");
    }

    [Fact]
    public void ToMaskedString_ShouldReturnMaskedCard_WhenCardTokenIsValid()
    {
        // Arrange
        var cardToken = new CardToken("token_1234567890", "1234", "Visa");

        // Act
        var masked = cardToken.ToMaskedString();

        // Assert
        masked.Should().Be("**** **** **** 1234");
    }

    [Theory]
    [InlineData("Visa")]
    [InlineData("Mastercard")]
    [InlineData("Amex")]
    [InlineData("Discover")]
    public void Constructor_ShouldAcceptValidCardBrands(string cardBrand)
    {
        // Act
        var cardToken = new CardToken("token_123", "1234", cardBrand);

        // Assert
        cardToken.CardBrand.Should().Be(cardBrand);
    }

    [Fact]
    public void CardToken_ShouldBeEqual_WhenAllPropertiesAreEqual()
    {
        // Arrange
        var cardToken1 = new CardToken("token_123", "1234", "Visa");
        var cardToken2 = new CardToken("token_123", "1234", "Visa");

        // Assert
        cardToken1.Should().Be(cardToken2);
        (cardToken1 == cardToken2).Should().BeTrue();
    }

    [Fact]
    public void CardToken_ShouldNotBeEqual_WhenPropertiesAreDifferent()
    {
        // Arrange
        var cardToken1 = new CardToken("token_123", "1234", "Visa");
        var cardToken2 = new CardToken("token_456", "5678", "Mastercard");

        // Assert
        cardToken1.Should().NotBe(cardToken2);
        (cardToken1 != cardToken2).Should().BeTrue();
    }
}

