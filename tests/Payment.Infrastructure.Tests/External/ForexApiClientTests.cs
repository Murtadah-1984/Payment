using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.External;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Payment.Infrastructure.Tests.External;

/// <summary>
/// Unit tests for ForexApiClient.
/// Tests external API integration, error handling, and response parsing.
/// </summary>
public class ForexApiClientTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<ForexApiClient>> _loggerMock;
    private readonly HttpClient _httpClient;
    private readonly ForexApiClient _service;

    public ForexApiClientTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<ForexApiClient>>();

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.exchangerate.host")
        };

        // Default configuration
        _configurationMock
            .Setup(c => c["Forex:BaseUrl"])
            .Returns("https://api.exchangerate.host");

        _configurationMock
            .Setup(c => c["Forex:ApiKey"])
            .Returns("test-api-key");

        _service = new ForexApiClient(_httpClient, _loggerMock.Object, _configurationMock.Object);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldReturnRateAndConvertedAmount_WhenApiSucceeds()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;
        var rate = 0.85m;
        var convertedAmount = 85.00m;

        var response = new
        {
            success = true,
            query = new { from = fromCurrency, to = toCurrency, amount = amount },
            info = new { rate = rate },
            result = convertedAmount
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);

        // Assert
        result.Rate.Should().Be(rate);
        result.ConvertedAmount.Should().Be(convertedAmount);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldReturnRateOne_WhenCurrenciesAreSame()
    {
        // Arrange
        var currency = "USD";
        var amount = 100.00m;

        // Act
        var result = await _service.GetExchangeRateAsync(currency, currency, amount);

        // Assert
        result.Rate.Should().Be(1.0m);
        result.ConvertedAmount.Should().Be(amount);
        
        // Should not make HTTP call for same currency
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never,
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldUseCaseInsensitiveCurrencyComparison()
    {
        // Arrange
        var currency1 = "USD";
        var currency2 = "usd";
        var amount = 100.00m;

        // Act
        var result = await _service.GetExchangeRateAsync(currency1, currency2, amount);

        // Assert
        result.Rate.Should().Be(1.0m);
        result.ConvertedAmount.Should().Be(amount);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldBuildCorrectUrl_WithApiKey()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;
        var rate = 0.85m;
        var convertedAmount = 85.00m;

        var response = new
        {
            success = true,
            query = new { from = fromCurrency, to = toCurrency, amount = amount },
            info = new { rate = rate },
            result = convertedAmount
        };

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response), req => capturedRequest = req);

        // Act
        await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.AbsoluteUri.Should().Contain("from=USD");
        capturedRequest.RequestUri.AbsoluteUri.Should().Contain("to=EUR");
        capturedRequest.RequestUri.AbsoluteUri.Should().Contain("amount=100");
        capturedRequest.RequestUri.AbsoluteUri.Should().Contain("apikey=test-api-key");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldBuildCorrectUrl_WithoutApiKey()
    {
        // Arrange
        _configurationMock
            .Setup(c => c["Forex:ApiKey"])
            .Returns((string?)null);

        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;
        var rate = 0.85m;
        var convertedAmount = 85.00m;

        var response = new
        {
            success = true,
            query = new { from = fromCurrency, to = toCurrency, amount = amount },
            info = new { rate = rate },
            result = convertedAmount
        };

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response), req => capturedRequest = req);

        // Act
        await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.AbsoluteUri.Should().NotContain("apikey");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldUseCustomBaseUrl_WhenConfigured()
    {
        // Arrange
        _configurationMock
            .Setup(c => c["Forex:BaseUrl"])
            .Returns("https://custom-api.example.com");

        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;
        var rate = 0.85m;
        var convertedAmount = 85.00m;

        var response = new
        {
            success = true,
            query = new { from = fromCurrency, to = toCurrency, amount = amount },
            info = new { rate = rate },
            result = convertedAmount
        };

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response), req => capturedRequest = req);

        var customHttpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://custom-api.example.com")
        };

        var customService = new ForexApiClient(customHttpClient, _loggerMock.Object, _configurationMock.Object);

        // Act
        await customService.GetExchangeRateAsync(fromCurrency, toCurrency, amount);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.AbsoluteUri.Should().StartWith("https://custom-api.example.com");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldThrowInvalidOperationException_WhenApiReturnsSuccessFalse()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;

        var response = new
        {
            success = false,
            error = new { code = "INVALID_CURRENCY", message = "Invalid currency code" }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act & Assert
        var action = async () => await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Forex API error*");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldThrowInvalidOperationException_WhenApiReturnsError()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "INVALID";
        var amount = 100.00m;

        var response = new
        {
            success = false,
            error = "Invalid currency code"
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act & Assert
        var action = async () => await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Forex API error*");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldThrowInvalidOperationException_WhenHttpRequestFails()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act & Assert
        var action = async () => await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to fetch exchange rate*");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldThrowInvalidOperationException_WhenResponseIsNotJson()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;

        SetupHttpResponse(HttpStatusCode.OK, "Invalid JSON response");

        // Act & Assert
        var action = async () => await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to parse Forex API response*");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldThrowInvalidOperationException_WhenResponseMissingRequiredFields()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;

        var response = new
        {
            success = true
            // Missing 'info' and 'result' fields
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act & Assert
        var action = async () => await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldThrowArgumentException_WhenFromCurrencyIsNull()
    {
        // Act & Assert
        var action = async () => await _service.GetExchangeRateAsync(null!, "EUR", 100m);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("fromCurrency");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldThrowArgumentException_WhenToCurrencyIsNull()
    {
        // Act & Assert
        var action = async () => await _service.GetExchangeRateAsync("USD", null!, 100m);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("toCurrency");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldThrowArgumentException_WhenAmountIsNegative()
    {
        // Act & Assert
        var action = async () => await _service.GetExchangeRateAsync("USD", "EUR", -100m);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("amount");
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldHandleLargeAmounts()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "KWD";
        var amount = 1000000.00m;
        var rate = 0.30m;
        var convertedAmount = 300000.00m;

        var response = new
        {
            success = true,
            query = new { from = fromCurrency, to = toCurrency, amount = amount },
            info = new { rate = rate },
            result = convertedAmount
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);

        // Assert
        result.Rate.Should().Be(rate);
        result.ConvertedAmount.Should().Be(convertedAmount);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldHandleDecimalPrecision()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "IQD";
        var amount = 1.00m;
        var rate = 1310.50m;
        var convertedAmount = 1310.50m;

        var response = new
        {
            success = true,
            query = new { from = fromCurrency, to = toCurrency, amount = amount },
            info = new { rate = rate },
            result = convertedAmount
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount);

        // Assert
        result.Rate.Should().Be(rate);
        result.ConvertedAmount.Should().Be(convertedAmount);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ShouldPassCancellationToken()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var amount = 100.00m;
        var cancellationToken = new CancellationToken();
        var rate = 0.85m;
        var convertedAmount = 85.00m;

        var response = new
        {
            success = true,
            query = new { from = fromCurrency, to = toCurrency, amount = amount },
            info = new { rate = rate },
            result = convertedAmount
        };

        CancellationToken? capturedToken = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                capturedToken = ct;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
                };
            });

        // Act
        await _service.GetExchangeRateAsync(fromCurrency, toCurrency, amount, cancellationToken);

        // Assert
        capturedToken.Should().Be(cancellationToken);
    }

    private void SetupHttpResponse(
        HttpStatusCode statusCode,
        string content,
        Action<HttpRequestMessage>? captureRequest = null)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                captureRequest?.Invoke(request);
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                };
            });
    }
}

