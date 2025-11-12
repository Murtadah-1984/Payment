using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

public class AmountTests
{
    [Fact]
    public void FromDecimal_ShouldCreateAmount_WhenValueIsPositive()
    {
        // Act
        var amount = Amount.FromDecimal(100.50m);

        // Assert
        amount.Value.Should().Be(100.50m);
    }

    [Fact]
    public void FromDecimal_ShouldThrowException_WhenValueIsZero()
    {
        // Act & Assert
        var act = () => Amount.FromDecimal(0m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Amount must be greater than zero*")
            .And.ParamName.Should().Be("value");
    }

    [Fact]
    public void FromDecimal_ShouldThrowException_WhenValueIsNegative()
    {
        // Act & Assert
        var act = () => Amount.FromDecimal(-10m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Amount must be greater than zero*")
            .And.ParamName.Should().Be("value");
    }

    [Fact]
    public void Amount_ShouldBeEqual_WhenValuesAreEqual()
    {
        // Arrange
        var amount1 = Amount.FromDecimal(100.50m);
        var amount2 = Amount.FromDecimal(100.50m);

        // Assert
        amount1.Should().Be(amount2);
        (amount1 == amount2).Should().BeTrue();
    }

    [Fact]
    public void Amount_ShouldNotBeEqual_WhenValuesAreDifferent()
    {
        // Arrange
        var amount1 = Amount.FromDecimal(100.50m);
        var amount2 = Amount.FromDecimal(200.50m);

        // Assert
        amount1.Should().NotBe(amount2);
        (amount1 != amount2).Should().BeTrue();
    }
}

