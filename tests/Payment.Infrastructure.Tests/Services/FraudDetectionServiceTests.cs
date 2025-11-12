using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Services;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Payment.Infrastructure.Tests.Services;

/// <summary>
/// Tests for FraudDetectionService (Fraud Detection #22).
/// </summary>
public class FraudDetectionServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<FraudDetectionService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly FraudDetectionService _service;

    public FraudDetectionServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<FraudDetectionService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.fraud-detection-service.com")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("FraudDetection"))
            .Returns(httpClient);

        // Default configuration: enabled with base URL and API key
        var baseUrlSection = new Mock<IConfigurationSection>();
        baseUrlSection.Setup(s => s.Value).Returns("https://api.fraud-detection-service.com");

        var apiKeySection = new Mock<IConfigurationSection>();
        apiKeySection.Setup(s => s.Value).Returns("test-api-key");

        var enabledSection = new Mock<IConfigurationSection>();
        enabledSection.Setup(s => s.Value).Returns("true");

        _configurationMock
            .Setup(c => c["FraudDetection:BaseUrl"])
            .Returns("https://api.fraud-detection-service.com");

        _configurationMock
            .Setup(c => c["FraudDetection:ApiKey"])
            .Returns("test-api-key");

        _configurationMock
            .Setup(c => c.GetValue<bool>("FraudDetection:Enabled", false))
            .Returns(true);

        _service = new FraudDetectionService(
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnLowRisk_WhenServiceReturnsLowRisk()
    {
        // Arrange
        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        var response = new
        {
            RiskLevel = "LOW",
            RiskScore = 0.1m,
            Recommendation = "Approve",
            Reasons = Array.Empty<string>(),
            TransactionId = "txn-123"
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Low);
        result.RiskScore.Should().Be(0.1m);
        result.Recommendation.Should().Be("Approve");
        result.TransactionId.Should().Be("txn-123");
        result.ShouldBlock.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnMediumRisk_WhenServiceReturnsMediumRisk()
    {
        // Arrange
        var request = FraudCheckRequest.Create(
            amount: 1000.00m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        var response = new
        {
            RiskLevel = "MEDIUM",
            RiskScore = 0.65m,
            Recommendation = "Review",
            Reasons = new[] { "Unusual amount", "New device" },
            TransactionId = "txn-456"
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Medium);
        result.RiskScore.Should().Be(0.65m);
        result.Recommendation.Should().Be("Review");
        result.Reasons.Should().HaveCount(2);
        result.Reasons.Should().Contain("Unusual amount");
        result.Reasons.Should().Contain("New device");
        result.ShouldReview.Should().BeTrue();
        result.ShouldBlock.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnHighRisk_WhenServiceReturnsHighRisk()
    {
        // Arrange
        var request = FraudCheckRequest.Create(
            amount: 5000.00m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        var response = new
        {
            RiskLevel = "HIGH",
            RiskScore = 0.95m,
            Recommendation = "Block",
            Reasons = new[] { "Suspicious IP", "Velocity check failed" },
            TransactionId = "txn-789"
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.High);
        result.RiskScore.Should().Be(0.95m);
        result.Recommendation.Should().Be("Block");
        result.Reasons.Should().HaveCount(2);
        result.ShouldBlock.Should().BeTrue();
        result.ShouldReview.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnLowRisk_WhenServiceIsDisabled()
    {
        // Arrange
        _configurationMock
            .Setup(c => c.GetValue<bool>("FraudDetection:Enabled", false))
            .Returns(false);

        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Low);
        result.RiskScore.Should().Be(0.0m);
        result.Recommendation.Should().Be("Approve");
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnLowRisk_WhenBaseUrlIsNotConfigured()
    {
        // Arrange
        _configurationMock
            .Setup(c => c["FraudDetection:BaseUrl"])
            .Returns((string?)null);

        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Low);
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnLowRisk_WhenApiKeyIsMissing()
    {
        // Arrange
        _configurationMock
            .Setup(c => c["FraudDetection:ApiKey"])
            .Returns((string?)null);

        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Low);
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnLowRisk_WhenServiceReturnsError()
    {
        // Arrange
        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Low);
        result.RiskScore.Should().Be(0.0m);
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnLowRisk_WhenServiceTimesOut()
    {
        // Arrange
        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Low);
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnLowRisk_WhenServiceThrowsException()
    {
        // Arrange
        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Low);
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnLowRisk_WhenResponseIsNull()
    {
        // Arrange
        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        SetupHttpResponse(HttpStatusCode.OK, "null");

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Low);
    }

    [Fact]
    public async Task CheckAsync_ShouldSendCorrectRequest_WhenCalled()
    {
        // Arrange
        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456",
            customerEmail: "customer@example.com",
            customerPhone: "+1234567890",
            customerId: "customer-123",
            deviceId: "device-456",
            ipAddress: "192.168.1.1",
            projectCode: "PROJECT-001");

        HttpRequestMessage? capturedRequest = null;
        var response = new
        {
            RiskLevel = "LOW",
            RiskScore = 0.1m,
            Recommendation = "Approve",
            Reasons = Array.Empty<string>()
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response), req => capturedRequest = req);

        // Act
        await _service.CheckAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.PathAndQuery.Should().Be("/api/v1/fraud/check");
        capturedRequest.Headers.Contains("X-API-Key").Should().BeTrue();
        capturedRequest.Headers.GetValues("X-API-Key").Should().Contain("test-api-key");
        capturedRequest.Headers.Contains("Accept").Should().BeTrue();
        capturedRequest.Headers.GetValues("Accept").Should().Contain("application/json");
    }

    [Fact]
    public async Task CheckAsync_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // Act & Assert
        var action = async () => await _service.CheckAsync(null!);
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    [Theory]
    [InlineData("LOW", FraudRiskLevel.Low)]
    [InlineData("low", FraudRiskLevel.Low)]
    [InlineData("Low", FraudRiskLevel.Low)]
    [InlineData("MEDIUM", FraudRiskLevel.Medium)]
    [InlineData("MED", FraudRiskLevel.Medium)]
    [InlineData("medium", FraudRiskLevel.Medium)]
    [InlineData("HIGH", FraudRiskLevel.High)]
    [InlineData("high", FraudRiskLevel.High)]
    [InlineData("UNKNOWN", FraudRiskLevel.Low)]
    [InlineData(null, FraudRiskLevel.Low)]
    public async Task CheckAsync_ShouldMapRiskLevelCorrectly(string? riskLevel, FraudRiskLevel expectedLevel)
    {
        // Arrange
        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        var response = new
        {
            RiskLevel = riskLevel,
            RiskScore = 0.5m,
            Recommendation = "Review",
            Reasons = Array.Empty<string>()
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _service.CheckAsync(request);

        // Assert
        result.RiskLevel.Should().Be(expectedLevel);
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

