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
/// Tests for AuthenticationMetricsMiddleware.
/// Verifies that authentication failures are properly recorded.
/// </summary>
public class AuthenticationMetricsMiddlewareTests
{
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly Mock<ILogger<AuthenticationMetricsMiddleware>> _loggerMock;
    private readonly Mock<IMetricsRecorder> _metricsRecorderMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly AuthenticationMetricsMiddleware _middleware;

    public AuthenticationMetricsMiddlewareTests()
    {
        _nextMock = new Mock<RequestDelegate>();
        _loggerMock = new Mock<ILogger<AuthenticationMetricsMiddleware>>();
        _metricsRecorderMock = new Mock<IMetricsRecorder>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_metricsRecorderMock.Object);
        _serviceProvider = serviceCollection.BuildServiceProvider();

        _middleware = new AuthenticationMetricsMiddleware(
            _nextMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotRecordMetrics_WhenStatusCodeIsNot401()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.StatusCode = 200;

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordAuthenticationFailure(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ShouldRecordMetrics_WhenStatusCodeIs401()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.StatusCode = 401;

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordAuthenticationFailure(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldRecordMissingToken_WhenAuthorizationHeaderIsMissing()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.StatusCode = 401;
        // No Authorization header set

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordAuthenticationFailure("missing_token"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldRecordInvalidToken_WhenAuthorizationHeaderIsPresent()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Response.StatusCode = 401;
        context.Request.Headers["Authorization"] = "Bearer invalid-token";

        _nextMock.Setup(next => next(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context, _serviceProvider);

        // Assert
        _metricsRecorderMock.Verify(
            r => r.RecordAuthenticationFailure("invalid_token"),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotThrow_WhenMetricsRecorderIsNotRegistered()
    {
        // Arrange
        var emptyServiceProvider = new ServiceCollection().BuildServiceProvider();
        var context = CreateHttpContext();
        context.Response.StatusCode = 401;

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

