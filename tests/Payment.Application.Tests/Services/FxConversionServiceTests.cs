using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Interfaces;
using Xunit;

namespace Payment.Application.Tests.Services;

/// <summary>
/// Unit tests for FxConversionService.
/// Tests currency conversion logic, error handling, and integration with ForexApiClient.
/// </summary>
public class FxConversionServiceTests
{
    private readonly Mock<IForexApiClient> _forexApiClientMock;
    private readonly Mock<ILogger<FxConversionService>> _loggerMock;
    private readonly FxConversionService _service;

    public FxConversionServiceTests()
    {
        _forexApiClientMock = new Mock<IForexApiClient>();
        _loggerMock = new Mock<ILogger<FxConversionService>>();
        _service = new FxConversionService(_forexApiClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ConvertAsync_ShouldReturnConversionResult_WhenConversionSucceeds()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;
        var rate = 0.85m;
        var convertedAmount = 85.00m;

        _forexApiClientMock
            .Setup(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, It.IsAny<CancellationToken>()))
            .ReturnsAsync((rate, convertedAmount));

        // Act
        var result = await _service.ConvertAsync(fromCurrency, toCurrency, amount);

        // Assert
        result.Should().NotBeNull();
        result.OriginalAmount.Should().Be(amount);
        result.ConvertedAmount.Should().Be(convertedAmount);
        result.FromCurrency.Should().Be(fromCurrency.ToUpperInvariant());
        result.ToCurrency.Should().Be(toCurrency.ToUpperInvariant());
        result.Rate.Should().Be(rate);
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _forexApiClientMock.Verify(
            x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConvertAsync_ShouldConvertCaseInsensitiveCurrencies()
    {
        // Arrange
        var fromCurrency = "usd";
        var toCurrency = "eur";
        var amount = 100.00m;
        var rate = 0.85m;
        var convertedAmount = 85.00m;

        _forexApiClientMock
            .Setup(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, It.IsAny<CancellationToken>()))
            .ReturnsAsync((rate, convertedAmount));

        // Act
        var result = await _service.ConvertAsync(fromCurrency, toCurrency, amount);

        // Assert
        result.FromCurrency.Should().Be("USD");
        result.ToCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task ConvertAsync_ShouldHandleLargeAmounts()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "KWD";
        var amount = 1000000.00m;
        var rate = 0.30m;
        var convertedAmount = 300000.00m;

        _forexApiClientMock
            .Setup(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, It.IsAny<CancellationToken>()))
            .ReturnsAsync((rate, convertedAmount));

        // Act
        var result = await _service.ConvertAsync(fromCurrency, toCurrency, amount);

        // Assert
        result.OriginalAmount.Should().Be(amount);
        result.ConvertedAmount.Should().Be(convertedAmount);
        result.Rate.Should().Be(rate);
    }

    [Fact]
    public async Task ConvertAsync_ShouldHandleSmallAmounts()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "IQD";
        var amount = 0.01m;
        var rate = 1310.00m;
        var convertedAmount = 13.10m;

        _forexApiClientMock
            .Setup(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, It.IsAny<CancellationToken>()))
            .ReturnsAsync((rate, convertedAmount));

        // Act
        var result = await _service.ConvertAsync(fromCurrency, toCurrency, amount);

        // Assert
        result.OriginalAmount.Should().Be(amount);
        result.ConvertedAmount.Should().Be(convertedAmount);
    }

    [Fact]
    public async Task ConvertAsync_ShouldHandleZeroAmount()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 0m;
        var rate = 0.85m;
        var convertedAmount = 0m;

        _forexApiClientMock
            .Setup(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, It.IsAny<CancellationToken>()))
            .ReturnsAsync((rate, convertedAmount));

        // Act
        var result = await _service.ConvertAsync(fromCurrency, toCurrency, amount);

        // Assert
        result.OriginalAmount.Should().Be(0m);
        result.ConvertedAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ConvertAsync_ShouldPassCancellationToken()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;
        var cancellationToken = new CancellationToken();
        var rate = 0.85m;
        var convertedAmount = 85.00m;

        _forexApiClientMock
            .Setup(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, cancellationToken))
            .ReturnsAsync((rate, convertedAmount));

        // Act
        await _service.ConvertAsync(fromCurrency, toCurrency, amount, cancellationToken);

        // Assert
        _forexApiClientMock.Verify(
            x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, cancellationToken),
            Times.Once);
    }

    [Theory]
    [InlineData("USD", "EUR")]
    [InlineData("EUR", "GBP")]
    [InlineData("KWD", "AED")]
    [InlineData("IQD", "USD")]
    [InlineData("SAR", "QAR")]
    public async Task ConvertAsync_ShouldHandleVariousCurrencyPairs(string from, string to)
    {
        // Arrange
        var amount = 100.00m;
        var rate = 1.5m;
        var convertedAmount = 150.00m;

        _forexApiClientMock
            .Setup(x => x.GetExchangeRateAsync(from, to, amount, It.IsAny<CancellationToken>()))
            .ReturnsAsync((rate, convertedAmount));

        // Act
        var result = await _service.ConvertAsync(from, to, amount);

        // Assert
        result.Should().NotBeNull();
        result.FromCurrency.Should().Be(from.ToUpperInvariant());
        result.ToCurrency.Should().Be(to.ToUpperInvariant());
    }

    [Fact]
    public async Task ConvertAsync_ShouldThrowArgumentException_WhenFromCurrencyIsNull()
    {
        // Act & Assert
        var action = async () => await _service.ConvertAsync(null!, "EUR", 100m);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("fromCurrency");
    }

    [Fact]
    public async Task ConvertAsync_ShouldThrowArgumentException_WhenFromCurrencyIsEmpty()
    {
        // Act & Assert
        var action = async () => await _service.ConvertAsync(string.Empty, "EUR", 100m);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("fromCurrency");
    }

    [Fact]
    public async Task ConvertAsync_ShouldThrowArgumentException_WhenFromCurrencyIsWhitespace()
    {
        // Act & Assert
        var action = async () => await _service.ConvertAsync("   ", "EUR", 100m);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("fromCurrency");
    }

    [Fact]
    public async Task ConvertAsync_ShouldThrowArgumentException_WhenToCurrencyIsNull()
    {
        // Act & Assert
        var action = async () => await _service.ConvertAsync("USD", null!, 100m);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("toCurrency");
    }

    [Fact]
    public async Task ConvertAsync_ShouldThrowArgumentException_WhenToCurrencyIsEmpty()
    {
        // Act & Assert
        var action = async () => await _service.ConvertAsync("USD", string.Empty, 100m);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("toCurrency");
    }

    [Fact]
    public async Task ConvertAsync_ShouldThrowArgumentException_WhenAmountIsNegative()
    {
        // Act & Assert
        var action = async () => await _service.ConvertAsync("USD", "EUR", -100m);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("amount");
    }

    [Fact]
    public async Task ConvertAsync_ShouldPropagateException_WhenForexApiClientThrows()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;

        _forexApiClientMock
            .Setup(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API error"));

        // Act & Assert
        var action = async () => await _service.ConvertAsync(fromCurrency, toCurrency, amount);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("API error");
    }

    [Fact]
    public async Task ConvertAsync_ShouldPropagateHttpException_WhenForexApiClientThrows()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;

        _forexApiClientMock
            .Setup(x => x.GetExchangeRateAsync(fromCurrency, toCurrency, amount, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act & Assert
        var action = async () => await _service.ConvertAsync(fromCurrency, toCurrency, amount);
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("Connection failed");
    }
}

