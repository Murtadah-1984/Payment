using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Middleware;
using System.Net;
using Xunit;

namespace Payment.API.Tests.Middleware;

public class RequestSanitizationMiddlewareTests
{
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly Mock<ILogger<RequestSanitizationMiddleware>> _loggerMock;
    private readonly RequestSanitizationMiddleware _middleware;

    public RequestSanitizationMiddlewareTests()
    {
        _nextMock = new Mock<RequestDelegate>();
        _loggerMock = new Mock<ILogger<RequestSanitizationMiddleware>>();

        _middleware = new RequestSanitizationMiddleware(
            _nextMock.Object,
            _loggerMock.Object);
    }

    private HttpContext CreateHttpContext(string path, bool isHttps = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Scheme = isHttps ? "https" : "http";
        context.Request.IsHttps = isHttps;
        return context;
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNext_WhenInvoked()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments");

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddXContentTypeOptionsHeader()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments");

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-Content-Type-Options");
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddXFrameOptionsHeader()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments");

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-Frame-Options");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddXXSSProtectionHeader()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments");

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-XSS-Protection");
        context.Response.Headers["X-XSS-Protection"].ToString().Should().Be("1; mode=block");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddReferrerPolicyHeader()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments");

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Referrer-Policy");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddContentSecurityPolicyHeader()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments");

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Content-Security-Policy");
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("script-src 'self'");
        csp.Should().Contain("frame-ancestors 'none'");
        csp.Should().Contain("upgrade-insecure-requests");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddPermissionsPolicyHeader()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments");

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Permissions-Policy");
        var permissionsPolicy = context.Response.Headers["Permissions-Policy"].ToString();
        permissionsPolicy.Should().Contain("geolocation=()");
        permissionsPolicy.Should().Contain("microphone=()");
        permissionsPolicy.Should().Contain("camera=()");
        permissionsPolicy.Should().Contain("payment=()");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddStrictTransportSecurityHeader_WhenHttps()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments", isHttps: true);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Strict-Transport-Security");
        var hsts = context.Response.Headers["Strict-Transport-Security"].ToString();
        hsts.Should().Contain("max-age=31536000");
        hsts.Should().Contain("includeSubDomains");
        hsts.Should().Contain("preload");
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotAddStrictTransportSecurityHeader_WhenHttp()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments", isHttps: false);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddAllSecurityHeaders_WhenMultipleRequests()
    {
        // Arrange
        var context1 = CreateHttpContext("/api/v1/payments");
        var context2 = CreateHttpContext("/api/v1/payments/123");

        // Act
        await _middleware.InvokeAsync(context1);
        await _middleware.InvokeAsync(context2);

        // Assert
        var requiredHeaders = new[]
        {
            "X-Content-Type-Options",
            "X-Frame-Options",
            "X-XSS-Protection",
            "Referrer-Policy",
            "Content-Security-Policy",
            "Permissions-Policy"
        };

        foreach (var header in requiredHeaders)
        {
            context1.Response.Headers.Should().ContainKey(header);
            context2.Response.Headers.Should().ContainKey(header);
        }

        _nextMock.Verify(next => next(It.IsAny<HttpContext>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotOverrideExistingHeaders()
    {
        // Arrange
        var context = CreateHttpContext("/api/v1/payments");
        context.Response.Headers["X-Content-Type-Options"] = "existing-value";

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        // The middleware should not override existing headers
        // However, our implementation checks if header exists before adding
        // So existing header should remain
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("existing-value");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddSecurityHeaders_ForAllEndpoints()
    {
        // Arrange
        var endpoints = new[]
        {
            "/api/v1/payments",
            "/api/v1/payments/123",
            "/health",
            "/ready",
            "/swagger"
        };

        // Act & Assert
        foreach (var endpoint in endpoints)
        {
            var context = CreateHttpContext(endpoint);
            await _middleware.InvokeAsync(context);

            context.Response.Headers.Should().ContainKey("X-Content-Type-Options");
            context.Response.Headers.Should().ContainKey("X-Frame-Options");
            context.Response.Headers.Should().ContainKey("Content-Security-Policy");
        }

        _nextMock.Verify(next => next(It.IsAny<HttpContext>()), Times.Exactly(endpoints.Length));
    }

    [Fact]
    public async Task InvokeAsync_ShouldHandleNullContext()
    {
        // Arrange
        HttpContext? context = null;

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(async () =>
            await _middleware.InvokeAsync(context!));
    }
}

