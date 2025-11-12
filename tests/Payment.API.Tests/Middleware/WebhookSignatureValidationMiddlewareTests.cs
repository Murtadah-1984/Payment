using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Middleware;
using Payment.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Payment.API.Tests.Middleware;

public class WebhookSignatureValidationMiddlewareTests
{
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly Mock<ILogger<WebhookSignatureValidationMiddleware>> _loggerMock;
    private readonly Mock<ICallbackSignatureValidator> _validatorMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly WebhookSignatureValidationMiddleware _middleware;

    public WebhookSignatureValidationMiddlewareTests()
    {
        _nextMock = new Mock<RequestDelegate>();
        _loggerMock = new Mock<ILogger<WebhookSignatureValidationMiddleware>>();
        _validatorMock = new Mock<ICallbackSignatureValidator>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_validatorMock.Object);
        _serviceProvider = serviceCollection.BuildServiceProvider();

        _middleware = new WebhookSignatureValidationMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _serviceProvider);
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNext_WhenNotCallbackEndpoint()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments/123");

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _nextMock.Verify(next => next(context), Times.Once);
        _validatorMock.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ShouldValidateSignature_WhenCallbackEndpoint()
    {
        // Arrange
        var provider = "zaincash";
        var context = CreateHttpContext($"/api/v1/payments/{provider}/callback");
        var payload = "test-payload";
        var signature = "valid-signature";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Headers["X-Signature"] = signature;
        context.Request.Headers["X-Timestamp"] = timestamp;

        _validatorMock
            .Setup(v => v.ValidateAsync(provider, payload, signature, timestamp))
            .ReturnsAsync(true);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _validatorMock.Verify(v => v.ValidateAsync(provider, payload, signature, timestamp), Times.Once);
        _nextMock.Verify(next => next(context), Times.Once);
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401_WhenSignatureIsInvalid()
    {
        // Arrange
        var provider = "fib";
        var context = CreateHttpContext($"/api/v1/payments/{provider}/callback");
        var payload = "test-payload";
        var signature = "invalid-signature";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Headers["X-Signature"] = signature;
        context.Request.Headers["X-Timestamp"] = timestamp;

        _validatorMock
            .Setup(v => v.ValidateAsync(provider, payload, signature, timestamp))
            .ReturnsAsync(false);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
        _nextMock.Verify(next => next(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn400_WhenProviderCannotBeExtracted()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments/callback");

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(400);
        _nextMock.Verify(next => next(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401_WhenTimestampIsExpired()
    {
        // Arrange
        var provider = "telr";
        var context = CreateHttpContext($"/api/v1/payments/{provider}/callback");
        var payload = "test-payload";
        var signature = "valid-signature";
        // Timestamp from 10 minutes ago (expired)
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Headers["X-Signature"] = signature;
        context.Request.Headers["X-Timestamp"] = expiredTimestamp;

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
        _nextMock.Verify(next => next(It.IsAny<HttpContext>()), Times.Never);
        _validatorMock.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ShouldAcceptValidTimestamp()
    {
        // Arrange
        var provider = "zaincash";
        var context = CreateHttpContext($"/api/v1/payments/{provider}/callback");
        var payload = "test-payload";
        var signature = "valid-signature";
        // Timestamp from 2 minutes ago (valid)
        var validTimestamp = DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds().ToString();

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Headers["X-Signature"] = signature;
        context.Request.Headers["X-Timestamp"] = validTimestamp;

        _validatorMock
            .Setup(v => v.ValidateAsync(provider, payload, signature, validTimestamp))
            .ReturnsAsync(true);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        _nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldHandleMultipleSignatureHeaderNames()
    {
        // Arrange
        var provider = "fib";
        var context = CreateHttpContext($"/api/v1/payments/{provider}/callback");
        var payload = "test-payload";
        var signature = "valid-signature";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        // Use alternative header name
        context.Request.Headers["X-Webhook-Signature"] = signature;
        context.Request.Headers["X-Timestamp"] = timestamp;

        _validatorMock
            .Setup(v => v.ValidateAsync(provider, payload, signature, timestamp))
            .ReturnsAsync(true);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _validatorMock.Verify(v => v.ValidateAsync(provider, payload, signature, timestamp), Times.Once);
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_ShouldHandleQueryStringInPath()
    {
        // Arrange
        var provider = "zaincash";
        var context = CreateHttpContext($"/api/v1/payments/{provider}/callback?token=abc123");
        var payload = "test-payload";
        var signature = "valid-signature";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Headers["X-Signature"] = signature;
        context.Request.Headers["X-Timestamp"] = timestamp;

        _validatorMock
            .Setup(v => v.ValidateAsync(provider, payload, signature, timestamp))
            .ReturnsAsync(true);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _validatorMock.Verify(v => v.ValidateAsync(provider, payload, signature, timestamp), Times.Once);
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_ShouldResetBodyStreamPosition_AfterReading()
    {
        // Arrange
        var provider = "telr";
        var context = CreateHttpContext($"/api/v1/payments/{provider}/callback");
        var payload = "test-payload";
        var signature = "valid-signature";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Body = bodyStream;
        context.Request.Headers["X-Signature"] = signature;
        context.Request.Headers["X-Timestamp"] = timestamp;

        _validatorMock
            .Setup(v => v.ValidateAsync(provider, payload, signature, timestamp))
            .ReturnsAsync(true);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        // Body stream should be reset to position 0 for downstream handlers
        bodyStream.Position.Should().Be(0);
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "POST";
        context.Response.Body = new MemoryStream();
        return context;
    }
}

