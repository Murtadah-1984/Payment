using FluentAssertions;
using Moq;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Metrics;
using Xunit;

namespace Payment.Infrastructure.Tests.Metrics;

/// <summary>
/// Comprehensive tests for MetricsRecorder implementation.
/// Tests all business, technical, and security metrics recording.
/// </summary>
public class MetricsRecorderTests
{
    private readonly MetricsRecorder _metricsRecorder;

    public MetricsRecorderTests()
    {
        _metricsRecorder = new MetricsRecorder();
    }

    #region Report Metrics Tests

    [Fact]
    public void RecordReportGenerated_ShouldNotThrow()
    {
        // Arrange
        var projectCode = "PROJECT1";
        var durationSeconds = 5.0;

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordReportGenerated(projectCode, durationSeconds))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordReportFailure_ShouldNotThrow()
    {
        // Arrange
        var projectCode = "PROJECT1";
        var errorType = "database_error";
        var durationSeconds = 2.0;

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordReportFailure(projectCode, errorType, durationSeconds))
            .Should().NotThrow();
    }

    #endregion

    #region Business Metrics Tests

    [Fact]
    public void RecordPaymentAttempt_ShouldNotThrow()
    {
        // Arrange
        var provider = "ZainCash";
        var status = "succeeded";
        var durationSeconds = 1.5;

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordPaymentAttempt(provider, status, durationSeconds))
            .Should().NotThrow();
    }

    [Fact]
    public void UpdateProviderAvailability_ShouldNotThrow_WhenAvailable()
    {
        // Arrange
        var provider = "FIB";
        var isAvailable = true;

        // Act & Assert
        _metricsRecorder.Invoking(r => r.UpdateProviderAvailability(provider, isAvailable))
            .Should().NotThrow();
    }

    [Fact]
    public void UpdateProviderAvailability_ShouldNotThrow_WhenUnavailable()
    {
        // Arrange
        var provider = "Telr";
        var isAvailable = false;

        // Act & Assert
        _metricsRecorder.Invoking(r => r.UpdateProviderAvailability(provider, isAvailable))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordRefund_ShouldNotThrow()
    {
        // Arrange
        var provider = "ZainCash";
        var status = "succeeded";
        var durationSeconds = 0.5;

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordRefund(provider, status, durationSeconds))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordFraudDetection_ShouldNotThrow()
    {
        // Arrange
        var result = "detected";
        var action = "blocked";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordFraudDetection(result, action))
            .Should().NotThrow();
    }

    #endregion

    #region Technical Metrics Tests

    [Fact]
    public void RecordApiResponseTime_ShouldNotThrow()
    {
        // Arrange
        var endpoint = "/api/v1/payments";
        var method = "POST";
        var statusCode = 200;
        var durationSeconds = 0.15;

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordApiResponseTime(endpoint, method, statusCode, durationSeconds))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordDatabaseQueryTime_ShouldNotThrow()
    {
        // Arrange
        var operation = "SELECT";
        var table = "Payments";
        var durationSeconds = 0.01;

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordDatabaseQueryTime(operation, table, durationSeconds))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordCacheOperation_ShouldNotThrow_ForHit()
    {
        // Arrange
        var operation = "get";
        var result = "hit";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordCacheOperation(operation, result))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordCacheOperation_ShouldNotThrow_ForMiss()
    {
        // Arrange
        var operation = "get";
        var result = "miss";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordCacheOperation(operation, result))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordError_ShouldNotThrow()
    {
        // Arrange
        var type = "payment_processing";
        var severity = "error";
        var component = "PaymentOrchestrator";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordError(type, severity, component))
            .Should().NotThrow();
    }

    [Fact]
    public void UpdateCircuitBreakerState_ShouldNotThrow_ForClosed()
    {
        // Arrange
        var provider = "ZainCash";
        var state = "closed";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.UpdateCircuitBreakerState(provider, state))
            .Should().NotThrow();
    }

    [Fact]
    public void UpdateCircuitBreakerState_ShouldNotThrow_ForOpen()
    {
        // Arrange
        var provider = "FIB";
        var state = "open";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.UpdateCircuitBreakerState(provider, state))
            .Should().NotThrow();
    }

    [Fact]
    public void UpdateCircuitBreakerState_ShouldNotThrow_ForHalfOpen()
    {
        // Arrange
        var provider = "Telr";
        var state = "half-open";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.UpdateCircuitBreakerState(provider, state))
            .Should().NotThrow();
    }

    [Fact]
    public void UpdateCircuitBreakerState_ShouldHandleCaseInsensitive()
    {
        // Arrange
        var provider = "ZainCash";
        var state = "HALFOPEN";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.UpdateCircuitBreakerState(provider, state))
            .Should().NotThrow();
    }

    [Fact]
    public void UpdateQueueDepth_ShouldNotThrow()
    {
        // Arrange
        var queueName = "payment-processing";
        var depth = 10;

        // Act & Assert
        _metricsRecorder.Invoking(r => r.UpdateQueueDepth(queueName, depth))
            .Should().NotThrow();
    }

    #endregion

    #region Security Metrics Tests

    [Fact]
    public void RecordAuthenticationFailure_ShouldNotThrow()
    {
        // Arrange
        var reason = "invalid_token";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordAuthenticationFailure(reason))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordRateLimitHit_ShouldNotThrow_WithIpAddress()
    {
        // Arrange
        var endpoint = "/api/v1/payments";
        var ipAddress = "192.168.1.1";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordRateLimitHit(endpoint, ipAddress))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordRateLimitHit_ShouldNotThrow_WithoutIpAddress()
    {
        // Arrange
        var endpoint = "/api/v1/payments";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordRateLimitHit(endpoint))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordWebhookSignatureFailure_ShouldNotThrow()
    {
        // Arrange
        var provider = "ZainCash";
        var reason = "invalid_signature";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordWebhookSignatureFailure(provider, reason))
            .Should().NotThrow();
    }

    [Fact]
    public void RecordSuspiciousActivity_ShouldNotThrow()
    {
        // Arrange
        var type = "fraud";
        var severity = "high";

        // Act & Assert
        _metricsRecorder.Invoking(r => r.RecordSuspiciousActivity(type, severity))
            .Should().NotThrow();
    }

    #endregion
}

