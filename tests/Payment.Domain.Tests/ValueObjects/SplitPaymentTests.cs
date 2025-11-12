using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

public class SplitPaymentTests
{
    [Fact]
    public void Calculate_ShouldCalculateCorrectly_WhenValidInput()
    {
        // Arrange
        var totalAmount = 100m;
        var systemFeePercent = 5m;

        // Act
        var splitPayment = SplitPayment.Calculate(totalAmount, systemFeePercent);

        // Assert
        splitPayment.SystemShare.Should().Be(5m);
        splitPayment.OwnerShare.Should().Be(95m);
        splitPayment.SystemFeePercent.Should().Be(5m);
        splitPayment.TotalAmount.Should().Be(100m);
    }

    [Fact]
    public void Calculate_ShouldRoundToTwoDecimals()
    {
        // Arrange
        var totalAmount = 100m;
        var systemFeePercent = 3.33m;

        // Act
        var splitPayment = SplitPayment.Calculate(totalAmount, systemFeePercent);

        // Assert
        splitPayment.SystemShare.Should().Be(3.33m);
        splitPayment.OwnerShare.Should().Be(96.67m);
    }

    [Fact]
    public void Calculate_ShouldThrowException_WhenTotalAmountIsZero()
    {
        // Act & Assert
        var act = () => SplitPayment.Calculate(0m, 5m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Total amount must be greater than zero*")
            .And.ParamName.Should().Be("totalAmount");
    }

    [Fact]
    public void Calculate_ShouldThrowException_WhenTotalAmountIsNegative()
    {
        // Act & Assert
        var act = () => SplitPayment.Calculate(-10m, 5m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Total amount must be greater than zero*")
            .And.ParamName.Should().Be("totalAmount");
    }

    [Fact]
    public void Calculate_ShouldThrowException_WhenSystemFeePercentIsNegative()
    {
        // Act & Assert
        var act = () => SplitPayment.Calculate(100m, -1m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("System fee percent must be between 0 and 100*")
            .And.ParamName.Should().Be("systemFeePercent");
    }

    [Fact]
    public void Calculate_ShouldThrowException_WhenSystemFeePercentIsGreaterThan100()
    {
        // Act & Assert
        var act = () => SplitPayment.Calculate(100m, 101m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("System fee percent must be between 0 and 100*")
            .And.ParamName.Should().Be("systemFeePercent");
    }

    [Fact]
    public void Calculate_ShouldHandleZeroFee()
    {
        // Act
        var splitPayment = SplitPayment.Calculate(100m, 0m);

        // Assert
        splitPayment.SystemShare.Should().Be(0m);
        splitPayment.OwnerShare.Should().Be(100m);
    }

    [Fact]
    public void Calculate_ShouldHandle100PercentFee()
    {
        // Act
        var splitPayment = SplitPayment.Calculate(100m, 100m);

        // Assert
        splitPayment.SystemShare.Should().Be(100m);
        splitPayment.OwnerShare.Should().Be(0m);
    }

    [Fact]
    public void TotalAmount_ShouldReturnSumOfSystemAndOwnerShare()
    {
        // Arrange
        var splitPayment = SplitPayment.Calculate(100m, 5m);

        // Act & Assert
        splitPayment.TotalAmount.Should().Be(splitPayment.SystemShare + splitPayment.OwnerShare);
    }
}

