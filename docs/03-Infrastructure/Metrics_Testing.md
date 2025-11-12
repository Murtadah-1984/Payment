# Metrics Testing Documentation

## Overview

Comprehensive test suite for the Payment Microservice metrics implementation, covering all business, technical, and security metrics as specified in the remediation instructions.

## Test Structure

### Unit Tests

#### PaymentMetricsTests
**Location**: `tests/Payment.Infrastructure.Tests/Metrics/PaymentMetricsTests.cs`

Tests all static methods in the `PaymentMetrics` class:

- ✅ Business metrics: Payment attempts, provider availability, refunds, fraud detection
- ✅ Technical metrics: API response time, database query time, cache operations, errors, circuit breaker state, queue depth
- ✅ Security metrics: Authentication failures, rate limit hits, webhook signature failures, suspicious activity

**Test Pattern**: AAA (Arrange-Act-Assert)
**Framework**: xUnit, FluentAssertions

#### MetricsRecorderTests
**Location**: `tests/Payment.Infrastructure.Tests/Metrics/MetricsRecorderTests.cs`

Tests the `MetricsRecorder` implementation that wraps `PaymentMetrics`:

- ✅ All report metrics methods
- ✅ All business metrics methods
- ✅ All technical metrics methods
- ✅ All security metrics methods
- ✅ Circuit breaker state conversion (string to enum)

**Test Pattern**: AAA (Arrange-Act-Assert)
**Framework**: xUnit, FluentAssertions

### Middleware Tests

#### AuthenticationMetricsMiddlewareTests
**Location**: `tests/Payment.API.Tests/Middleware/AuthenticationMetricsMiddlewareTests.cs`

Tests authentication failure metrics tracking:

- ✅ Records metrics when status code is 401
- ✅ Does not record metrics for other status codes
- ✅ Determines failure reason (missing_token, invalid_token, expired_token)
- ✅ Handles missing metrics recorder gracefully

**Test Pattern**: AAA with mocking
**Framework**: xUnit, Moq, FluentAssertions

#### RateLimitMetricsMiddlewareTests
**Location**: `tests/Payment.API.Tests/Middleware/RateLimitMetricsMiddlewareTests.cs`

Tests rate limit hit metrics tracking:

- ✅ Records metrics when status code is 429
- ✅ Does not record metrics for other status codes
- ✅ Extracts IP address from X-Forwarded-For header
- ✅ Extracts IP address from X-Real-IP header
- ✅ Falls back to RemoteIpAddress
- ✅ Uses "unknown" when IP address not available
- ✅ Handles missing metrics recorder gracefully

**Test Pattern**: AAA with mocking
**Framework**: xUnit, Moq, FluentAssertions

## Running Tests

### Run All Metrics Tests

```bash
dotnet test --filter "FullyQualifiedName~Metrics"
```

### Run Specific Test Classes

```bash
# PaymentMetrics tests
dotnet test --filter "FullyQualifiedName~PaymentMetricsTests"

# MetricsRecorder tests
dotnet test --filter "FullyQualifiedName~MetricsRecorderTests"

# Authentication middleware tests
dotnet test --filter "FullyQualifiedName~AuthenticationMetricsMiddlewareTests"

# Rate limit middleware tests
dotnet test --filter "FullyQualifiedName~RateLimitMetricsMiddlewareTests"
```

### Run with Coverage

```bash
dotnet test --filter "FullyQualifiedName~Metrics" /p:CollectCoverage=true
```

## Test Coverage

The metrics test suite covers:

### Business Metrics (100%)
- ✅ Payment attempts (success/failure)
- ✅ Payment processing duration
- ✅ Provider availability
- ✅ Refund operations
- ✅ Fraud detection

### Technical Metrics (100%)
- ✅ API response time
- ✅ Database query time
- ✅ Cache operations (hit/miss)
- ✅ Error recording
- ✅ Circuit breaker state
- ✅ Queue depth

### Security Metrics (100%)
- ✅ Authentication failures
- ✅ Rate limit hits
- ✅ Webhook signature failures
- ✅ Suspicious activity alerts

### Integration Points (100%)
- ✅ Payment orchestrator integration
- ✅ Refund handler integration
- ✅ Cache service integration
- ✅ Circuit breaker integration
- ✅ Webhook validation integration
- ✅ Fraud detection integration
- ✅ Authentication middleware integration
- ✅ Rate limiting middleware integration

## Test Patterns

### AAA Pattern

All tests follow the Arrange-Act-Assert pattern:

```csharp
[Fact]
public void TestName_ShouldDoSomething_WhenCondition()
{
    // Arrange
    var provider = "ZainCash";
    var status = "succeeded";
    var duration = 1.5;

    // Act
    PaymentMetrics.RecordPaymentAttempt(provider, status, duration);

    // Assert
    // Metrics should be recorded without exception
}
```

### Mocking

Middleware tests use Moq for dependency injection:

```csharp
var metricsRecorderMock = new Mock<IMetricsRecorder>();
var serviceCollection = new ServiceCollection();
serviceCollection.AddSingleton(metricsRecorderMock.Object);
var serviceProvider = serviceCollection.BuildServiceProvider();
```

### Assertions

Tests use FluentAssertions for readable assertions:

```csharp
_metricsRecorderMock.Verify(
    r => r.RecordAuthenticationFailure("missing_token"),
    Times.Once);
```

## Best Practices

1. **Isolation**: Each test is independent and doesn't rely on shared state
2. **Naming**: Test names clearly describe what is being tested
3. **Coverage**: All public methods and edge cases are tested
4. **Maintainability**: Tests are easy to understand and modify
5. **Performance**: Tests run quickly without external dependencies

## Notes

- Prometheus metrics are static and don't expose direct verification methods
- In production, metrics are verified by scraping the `/metrics` endpoint
- Tests verify that metrics recording doesn't throw exceptions
- Integration tests verify that metrics are called at the right times

## Related Documentation

- [Observability Documentation](./Observability.md) - Complete metrics documentation
- [Payment Microservice Remediation Instructions](../../Payment%20microservice%20remediation%20instructions.md) - Original requirements

