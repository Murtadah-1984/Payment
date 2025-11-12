using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Services;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Payment.Infrastructure.Tests.Services;

public class WebhookDeliveryServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IWebhookDeliveryRepository> _repositoryMock;
    private readonly Mock<ILogger<WebhookDeliveryService>> _loggerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly WebhookDeliveryService _service;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public WebhookDeliveryServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _repositoryMock = new Mock<IWebhookDeliveryRepository>();
        _loggerMock = new Mock<ILogger<WebhookDeliveryService>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://example.com")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("WebhookDelivery"))
            .Returns(httpClient);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new WebhookDeliveryService(
            _httpClientFactoryMock.Object,
            _repositoryMock.Object,
            _loggerMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task SendWebhookAsync_ShouldReturnSuccess_WhenHttpResponseIsSuccess()
    {
        // Arrange
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        SetupHttpResponse(HttpStatusCode.OK, "Success");

        // Act
        var result = await _service.SendWebhookAsync(webhookUrl, eventType, payload);

        // Assert
        result.Success.Should().BeTrue();
        result.HttpStatusCode.Should().Be(200);
        result.ErrorMessage.Should().BeNull();
        result.ResponseTime.Should().NotBeNull();
    }

    [Fact]
    public async Task SendWebhookAsync_ShouldReturnFailure_WhenHttpResponseIsNotSuccess()
    {
        // Arrange
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

        // Act
        var result = await _service.SendWebhookAsync(webhookUrl, eventType, payload);

        // Assert
        result.Success.Should().BeFalse();
        result.HttpStatusCode.Should().Be(500);
        result.ErrorMessage.Should().Contain("HTTP 500");
    }

    [Fact]
    public async Task SendWebhookAsync_ShouldReturnFailure_WhenRequestTimesOut()
    {
        // Arrange
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        // Act
        var result = await _service.SendWebhookAsync(webhookUrl, eventType, payload);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timeout", StringComparison.OrdinalIgnoreCase);
        result.HttpStatusCode.Should().BeNull();
    }

    [Fact]
    public async Task SendWebhookAsync_ShouldReturnFailure_WhenHttpRequestExceptionOccurs()
    {
        // Arrange
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act
        var result = await _service.SendWebhookAsync(webhookUrl, eventType, payload);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection failed");
    }

    [Fact]
    public async Task SendWebhookAsync_ShouldIncludeCustomHeaders_WhenProvided()
    {
        // Arrange
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";
        var headers = new Dictionary<string, string>
        {
            { "X-Custom-Header", "custom-value" },
            { "Authorization", "Bearer token123" }
        };

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, "Success", request => capturedRequest = request);

        // Act
        await _service.SendWebhookAsync(webhookUrl, eventType, payload, headers);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Contains("X-Custom-Header").Should().BeTrue();
        capturedRequest.Headers.Contains("Authorization").Should().BeTrue();
        capturedRequest.Headers.GetValues("X-Event-Type").Should().Contain(eventType);
    }

    [Fact]
    public async Task SendWebhookAsync_ShouldSetCorrectContentType()
    {
        // Arrange
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, "Success", request => capturedRequest = request);

        // Act
        await _service.SendWebhookAsync(webhookUrl, eventType, payload);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Content.Should().NotBeNull();
        capturedRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task ScheduleWebhookAsync_ShouldCreateWebhookDelivery_AndAttemptImmediateDelivery()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        SetupHttpResponse(HttpStatusCode.OK, "Success");

        WebhookDelivery? capturedWebhook = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookDelivery, CancellationToken>((w, ct) => capturedWebhook = w)
            .ReturnsAsync((WebhookDelivery w, CancellationToken ct) => w);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var webhookId = await _service.ScheduleWebhookAsync(paymentId, webhookUrl, eventType, payload);

        // Assert
        webhookId.Should().NotBeEmpty();
        capturedWebhook.Should().NotBeNull();
        capturedWebhook!.PaymentId.Should().Be(paymentId);
        capturedWebhook.WebhookUrl.Should().Be(webhookUrl);
        capturedWebhook.EventType.Should().Be(eventType);
        capturedWebhook.Payload.Should().Be(payload);
        capturedWebhook.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ScheduleWebhookAsync_ShouldMarkAsFailed_WhenImmediateDeliveryFails()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

        WebhookDelivery? capturedWebhook = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookDelivery, CancellationToken>((w, ct) => capturedWebhook = w)
            .ReturnsAsync((WebhookDelivery w, CancellationToken ct) => w);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var webhookId = await _service.ScheduleWebhookAsync(paymentId, webhookUrl, eventType, payload);

        // Assert
        webhookId.Should().NotBeEmpty();
        capturedWebhook.Should().NotBeNull();
        capturedWebhook!.Status.Should().Be(WebhookDeliveryStatus.Pending);
        capturedWebhook.RetryCount.Should().Be(1);
        capturedWebhook.NextRetryAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ScheduleWebhookAsync_ShouldUseCustomMaxRetries_WhenProvided()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";
        var maxRetries = 10;

        SetupHttpResponse(HttpStatusCode.OK, "Success");

        WebhookDelivery? capturedWebhook = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookDelivery, CancellationToken>((w, ct) => capturedWebhook = w)
            .ReturnsAsync((WebhookDelivery w, CancellationToken ct) => w);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ScheduleWebhookAsync(paymentId, webhookUrl, eventType, payload, maxRetries);

        // Assert
        capturedWebhook.Should().NotBeNull();
        capturedWebhook!.MaxRetries.Should().Be(10);
    }

    [Fact]
    public async Task ScheduleWebhookAsync_ShouldThrowException_WhenPaymentIdIsEmpty()
    {
        // Arrange
        var paymentId = Guid.Empty;
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        // Act & Assert
        var action = async () => await _service.ScheduleWebhookAsync(paymentId, webhookUrl, eventType, payload);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Payment ID cannot be empty*");
    }

    [Fact]
    public async Task ScheduleWebhookAsync_ShouldThrowException_WhenWebhookUrlIsInvalid()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var webhookUrl = "not-a-valid-url";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        // Act & Assert
        var action = async () => await _service.ScheduleWebhookAsync(paymentId, webhookUrl, eventType, payload);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Webhook URL cannot be null or empty*");
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

