using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Middleware;
using Payment.Domain.Interfaces;
using Xunit;

namespace Payment.API.Tests.Middleware;

/// <summary>
/// Tests for RateLimitMetricsMiddleware.
/// Verifies that rate limit hits are properly recorded.
/// </summary>
public class RateLimitMetricsMiddlewareTests
{
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly Mock<ILogger<RateLimitMetricsMiddleware>> _loggerMock;
    private readonly Mock<IMetricsRecorder> _metricsRecorderMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly RateLimitMetricsMiddleware _middleware;

    public RateLimitMetricsMiddlewareTests()
    {
        _nextMock = new Mock<RequestDelegate>();
        _loggerMock = new Mock<ILogger<RateLimitMetricsMiddleware>>();
        _metricsRecorderMock = new Mock<IMetricsRecorder>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_metricsRecorderMock.Object);
        _serviceProvider = serviceCollection.BuildServiceProvider();

        _middleware = new RateLimitMetricsMiddleware(
            _nextMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotRecordMetrics_WhenStatusCodeIsNot429()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.StatusCode = 200;

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordRateLimitHit(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ShouldRecordMetrics_WhenStatusCodeIs429()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments");
        context.Response.StatusCode = 429;

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordRateLimitHit(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldRecordEndpoint_WhenRateLimitHit()
    {
        // Arrange
        var endpoint = "/api/v1/payments";
        var context = CreateHttpContext(endpoint);
        context.Response.StatusCode = 429;

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordRateLimitHit(endpoint, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldExtractIpAddress_FromXForwardedFor()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.StatusCode = 429;
        context.Request.Headers["X-Forwarded-For"] = "192.168.1.1, 10.0.0.1";

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordRateLimitHit(It.IsAny<string>(), "192.168.1.1"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldExtractIpAddress_FromXRealIP()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.StatusCode = 429;
        context.Request.Headers["X-Real-IP"] = "10.0.0.1";

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordRateLimitHit(It.IsAny<string>(), "10.0.0.1"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseRemoteIpAddress_WhenHeadersNotPresent()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.StatusCode = 429;
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.16.0.1");

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordRateLimitHit(It.IsAny<string>(), "172.16.0.1"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseUnknown_WhenIpAddressNotAvailable()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.StatusCode = 429;
        context.Connection.RemoteIpAddress = null;

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordRateLimitHit(It.IsAny<string>(), "unknown"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotThrow_WhenMetricsRecorderIsNotRegistered()
    {
        // Arrange
        var emptyServiceProvider = new ServiceCollection().BuildServiceProvider();
        var context = CreateHttpContext();
        context.Response.StatusCode = 429;

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act & Assert
        await _middleware.Invoking(m => m.InvokeAsync(context, emptyServiceProvider))
            .Should().NotThrowAsync();
    }

    private static HttpContext CreateHttpContext(string path = "/api/v1/payments")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";
        return context;
    }
}

