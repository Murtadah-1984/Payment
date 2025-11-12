---
title: Performance & Optimization
version: 1.0
last_updated: 2025-11-11
category: Infrastructure
tags:
  - performance
  - caching
  - database
  - resilience
  - rate-limiting
  - state-machine
summary: >
  Performance optimization features including resilience patterns, caching strategy,
  database optimization, event sourcing, API versioning, and state machine.
related_docs:
  - Kubernetes_Deployment.md
  - Observability.md
ai_context_priority: medium
---

# 🚀 Performance & Optimization

## Resilience Patterns (HIGH Priority Performance Feature)

The Payment Microservice implements **resilience patterns** using Polly to handle payment provider failures gracefully.

### Features

- ✅ **Circuit Breaker**: Opens after 5 consecutive failures, stays open for 60 seconds
- ✅ **Retry with Exponential Backoff**: Retries up to 3 times with exponential backoff (2s, 4s, 8s)
- ✅ **Timeout Policy**: 30-second timeout for all payment provider calls
- ✅ **Graceful Degradation**: Returns user-friendly error messages when providers are unavailable

### Policy Configuration

```csharp
// Timeout: 30 seconds
var timeoutPolicy = Policy.TimeoutAsync<PaymentResult>(TimeSpan.FromSeconds(30));

// Retry: 3 times with exponential backoff
var retryPolicy = Policy<PaymentResult>
    .Handle<HttpRequestException>()
    .Or<TimeoutRejectedException>()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

// Circuit Breaker: Open after 5 failures, stay open for 60 seconds
var circuitBreakerPolicy = Policy<PaymentResult>
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(60));
```

### How It Works

All payment providers are automatically wrapped with the `ResilientPaymentProviderDecorator`, which applies these policies:

1. **Timeout**: If a provider call exceeds 30 seconds, it's cancelled
2. **Retry**: Transient failures (network errors, timeouts) trigger automatic retries
3. **Circuit Breaker**: After 5 consecutive failures, the circuit opens and all requests fail fast for 60 seconds
4. **Recovery**: After 60 seconds, the circuit enters "half-open" state to test if the provider has recovered

### Monitoring

- Circuit breaker state changes are logged
- Retry attempts are logged with warnings
- Timeout events are logged as errors
- Circuit breaker metrics can be exported to monitoring systems

## Caching Strategy (HIGH Priority Performance Feature)

The Payment Microservice implements **distributed caching** to improve performance and reduce database load.

### Features

- ✅ **Redis Support**: Uses Redis for distributed caching in production
- ✅ **Memory Cache Fallback**: Falls back to in-memory cache when Redis is unavailable
- ✅ **Automatic Cache-Aside**: Handlers automatically check cache before database queries
- ✅ **Configurable TTL**: Cache entries expire after configurable time (default: 5 minutes for payments)

### Cache Implementation

The `ICacheService` interface provides a unified caching API:

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
```

### Usage Example

```csharp
// In GetPaymentByIdQueryHandler
var cacheKey = $"payment:{paymentId}";

// Try cache first
var cached = await _cache.GetAsync<PaymentDto>(cacheKey, cancellationToken);
if (cached != null) return cached;

// Fetch from database
var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId, cancellationToken);
var dto = payment.ToDto();

// Cache for 5 minutes
await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
return dto;
```

### Cache Keys

- Payment by ID: `payment:{paymentId}`
- Provider configurations: `provider:{providerName}:config`
- Custom keys can be added as needed

### Configuration

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"  // Optional - falls back to memory cache if not provided
  }
}
```

### Cache Invalidation

Cache entries should be invalidated when data changes:

```csharp
// After updating a payment
await _cache.RemoveAsync($"payment:{paymentId}", cancellationToken);
```

## Database Optimization (HIGH Priority Performance Feature)

The Payment Microservice implements **comprehensive database optimizations** for improved query performance.

### Indexes

The following indexes have been added to optimize common queries:

- **IX_Payments_OrderId** (Unique): Fast lookups by order ID
- **IX_Payments_MerchantId**: Fast filtering by merchant
- **IX_Payments_Status**: Fast filtering by payment status
- **IX_Payments_TransactionId**: Fast lookups by transaction ID
- **IX_Payments_Merchant_Status_Date** (Composite): Optimizes queries filtering by merchant, status, and date range
- **IX_Payments_CreatedAt**: Efficient date range queries
- **IX_Payments_UpdatedAt**: Efficient date range queries

### Pagination Support

The `IPaymentRepository` now supports pagination for large result sets:

```csharp
// Get paginated payments by merchant
var result = await _paymentRepository.GetPagedByMerchantIdAsync(
    merchantId: "merchant-123",
    pageNumber: 1,
    pageSize: 20,
    cancellationToken);

// Result includes:
// - Items: List of payments
// - TotalCount: Total number of payments
// - PageNumber: Current page
// - PageSize: Items per page
// - TotalPages: Total number of pages
// - HasPreviousPage: Boolean
// - HasNextPage: Boolean
```

### Query Optimization

- **Ordered results**: All paginated queries are ordered by `CreatedAt DESC` for consistent results
- **Efficient counting**: Uses `CountAsync()` for accurate total counts
- **Skip/Take**: Uses EF Core's `Skip()` and `Take()` for efficient pagination

### Best Practices

1. **Use pagination** for all list endpoints to prevent large result sets
2. **Index frequently queried columns** to improve query performance
3. **Use composite indexes** for common query patterns (e.g., merchant + status + date)
4. **Monitor slow queries** using EF Core query logging in development
5. **Use compiled queries** for hot paths (can be added in future optimizations)

## Event Sourcing & Outbox Pattern (MEDIUM Priority Improvement)

The Payment Microservice implements the **Outbox Pattern** to ensure reliable event publishing for domain events. This guarantees that domain events are published even if the message broker is temporarily unavailable.

### How It Works

1. **Domain Events Captured**: When a domain event is raised (e.g., `PaymentCompletedEvent`), it's automatically captured in the same database transaction.
2. **Outbox Storage**: Domain events are saved to the `OutboxMessages` table in the same transaction as the payment entity.
3. **Background Processing**: The `OutboxProcessorService` background service processes pending outbox messages every 5 seconds.
4. **Event Publishing**: Events are published to the message broker (RabbitMQ/Azure Service Bus/Kafka) via the `IEventPublisher` interface.
5. **Retry Logic**: Failed events are retried up to 5 times with error tracking.
6. **Dead Letter Queue**: Events that exceed max retries are logged for manual intervention.

### Architecture

```csharp
// Domain events are automatically saved to outbox in UnitOfWork.SaveChangesAsync
public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
{
    // Get domain events from entities
    var domainEvents = _context.ChangeTracker
        .Entries<Entity>()
        .Where(e => e.Entity.DomainEvents.Any())
        .SelectMany(e => e.Entity.DomainEvents)
        .ToList();
    
    // Save changes first
    var result = await _context.SaveChangesAsync(cancellationToken);
    
    // Save events to outbox in same transaction
    foreach (var domainEvent in domainEvents)
    {
        OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(domainEvent),
            Topic = GetTopicForEvent(domainEvent),
            CreatedAt = DateTime.UtcNow
        });
    }
    
    // Clear events from entities
    _context.ChangeTracker.Entries<Entity>()
        .ToList()
        .ForEach(e => e.Entity.ClearDomainEvents());
    
    // Save outbox messages
    if (domainEvents.Any())
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
    
    return result;
}
```

### Outbox Message Entity

```csharp
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}
```

### Background Service

The `OutboxProcessorService` runs as a background service and:
- Processes pending messages in batches of 100
- Retries failed messages up to 5 times
- Logs errors for messages that exceed max retries
- Runs every 5 seconds

### Benefits

- ✅ **Guaranteed Delivery**: Events are persisted before publishing, ensuring they're not lost
- ✅ **Transactional Consistency**: Events are saved in the same transaction as domain changes
- ✅ **Resilience**: Automatic retry logic handles temporary message broker failures
- ✅ **Monitoring**: Failed events are tracked with error messages and retry counts
- ✅ **Scalability**: Batch processing handles high event volumes efficiently

## API Versioning (MEDIUM Priority Improvement)

The Payment Microservice implements **URL-based API versioning** to support multiple API versions simultaneously, enabling backward compatibility and gradual migration.

### Versioning Strategy

The API supports versioning through multiple methods:

1. **URL Segment** (Primary): `/api/v1/payments`, `/api/v2/payments`
2. **Header**: `X-Version: 1.0`
3. **Query String**: `/api/payments?version=1.0`

### Configuration

```csharp
// src/Payment.API/Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Version"),
        new QueryStringApiVersionReader("version"));
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

### Controller Implementation

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class PaymentsController : ControllerBase
{
    // v1 endpoints
}
```

### Swagger Integration

Swagger automatically shows all API versions:

```csharp
builder.Services.AddSwaggerGen(c =>
{
    var apiVersionDescriptionProvider = builder.Services.BuildServiceProvider()
        .GetRequiredService<IApiVersionDescriptionProvider>();

    foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
    {
        c.SwaggerDoc(description.GroupName, new OpenApiInfo
        {
            Title = "Payment Microservice API",
            Version = description.ApiVersion.ToString()
        });
    }
});
```

### Response Headers

All API responses include version information:

- `api-supported-versions`: Lists all supported API versions
- `api-deprecated-versions`: Lists deprecated versions (if any)

### Example Usage

**URL-based versioning:**
```http
GET /api/v1/payments/providers
GET /api/v2/payments/providers
```

**Header-based versioning:**
```http
GET /api/payments/providers
X-Version: 1.0
```

**Query string versioning:**
```http
GET /api/payments/providers?version=1.0
```

### Benefits

- ✅ **Backward Compatibility**: Multiple versions can coexist
- ✅ **Gradual Migration**: Clients can migrate at their own pace
- ✅ **Clear Versioning**: URL-based versioning is explicit and RESTful
- ✅ **Flexible**: Supports multiple versioning methods
- ✅ **Swagger Support**: All versions are documented in Swagger UI

## State Machine for Payment Status (MEDIUM Priority Improvement)

The Payment Microservice implements a **State Machine** using the **Stateless** library to ensure valid payment status transitions and prevent invalid operations.

### Features

- ✅ **Stateless Library**: Industry-standard state machine library
- ✅ **Valid State Transitions**: Enforces business rules for state changes
- ✅ **Invalid Transition Prevention**: Throws exceptions for invalid transitions
- ✅ **State Transition Logging**: Logs all state transitions for audit
- ✅ **Domain Service Integration**: `IPaymentStateService` bridges Domain and Infrastructure
- ✅ **Entity Integration**: Payment entity methods use state machine validation

### State Machine Configuration

The state machine defines all valid transitions:

```csharp
// Initiated → Processing, Failed, Cancelled
_stateMachine.Configure(PaymentStatus.Initiated)
    .Permit(PaymentTrigger.Process, PaymentStatus.Processing)
    .Permit(PaymentTrigger.Fail, PaymentStatus.Failed)
    .Permit(PaymentTrigger.Cancel, PaymentStatus.Cancelled);

// Pending → Processing, Failed, Cancelled
_stateMachine.Configure(PaymentStatus.Pending)
    .Permit(PaymentTrigger.Process, PaymentStatus.Processing)
    .Permit(PaymentTrigger.Fail, PaymentStatus.Failed)
    .Permit(PaymentTrigger.Cancel, PaymentStatus.Cancelled);

// Processing → Succeeded, Failed
_stateMachine.Configure(PaymentStatus.Processing)
    .Permit(PaymentTrigger.Complete, PaymentStatus.Succeeded)
    .Permit(PaymentTrigger.Fail, PaymentStatus.Failed);

// Succeeded → Refunded, PartiallyRefunded
_stateMachine.Configure(PaymentStatus.Succeeded)
    .Permit(PaymentTrigger.Refund, PaymentStatus.Refunded)
    .Permit(PaymentTrigger.PartialRefund, PaymentStatus.PartiallyRefunded);

// PartiallyRefunded → Refunded
_stateMachine.Configure(PaymentStatus.PartiallyRefunded)
    .Permit(PaymentTrigger.Refund, PaymentStatus.Refunded);
```

### Valid State Transitions

```
Initiated ──Process──> Processing ──Complete──> Succeeded ──Refund──> Refunded
    │                      │                          │
    │                      │                          └──PartialRefund──> PartiallyRefunded ──Refund──> Refunded
    │                      │
    └──Fail──> Failed     └──Fail──> Failed
    │
    └──Cancel──> Cancelled
```

### Usage in Payment Entity

Payment entity methods use the state machine service:

```csharp
public void Process(string transactionId, IPaymentStateService stateService)
{
    // Use state machine to validate and transition
    Status = stateService.Transition(Status, PaymentTrigger.Process);
    TransactionId = transactionId;
    UpdatedAt = DateTime.UtcNow;
    
    AddDomainEvent(new PaymentProcessingEvent(Id.Value, OrderId));
}

public void Complete(IPaymentStateService stateService)
{
    // Use state machine to validate and transition
    Status = stateService.Transition(Status, PaymentTrigger.Complete);
    UpdatedAt = DateTime.UtcNow;
    
    AddDomainEvent(new PaymentCompletedEvent(Id.Value, OrderId, Amount.Value, Currency.Code));
}
```

### Domain Service Interface

The Domain layer defines the state service interface (Dependency Inversion Principle):

```csharp
public interface IPaymentStateService
{
    PaymentStatus Transition(PaymentStatus currentStatus, PaymentTrigger trigger);
    bool CanTransition(PaymentStatus currentStatus, PaymentTrigger trigger);
}
```

### Infrastructure Implementation

The Infrastructure layer implements the state machine:

```csharp
public class PaymentStateService : IPaymentStateService
{
    public PaymentStatus Transition(PaymentStatus currentStatus, PaymentTrigger trigger)
    {
        var stateMachine = _stateMachineFactory.Create(currentStatus);
        
        if (!stateMachine.CanFire(trigger))
        {
            throw new InvalidOperationException(
                $"Cannot transition from {currentStatus} using trigger {trigger}");
        }
        
        stateMachine.Fire(trigger);
        return stateMachine.CurrentState;
    }
}
```

### Benefits

- ✅ **Prevents Invalid Transitions**: Business rules enforced at compile time
- ✅ **Clear State Flow**: Visual representation of valid transitions
- ✅ **Error Prevention**: Invalid operations throw exceptions immediately
- ✅ **Audit Trail**: All state transitions are logged
- ✅ **Testability**: Easy to test all valid and invalid transitions
- ✅ **Maintainability**: State rules centralized in one place

### Testing

Comprehensive unit tests ensure state machine works correctly:

```bash
dotnet test tests/Payment.Infrastructure.Tests/StateMachines/PaymentStateMachineTests.cs
dotnet test tests/Payment.Infrastructure.Tests/StateMachines/PaymentStateMachineInitiatedStateTests.cs
dotnet test tests/Payment.Infrastructure.Tests/Services/PaymentStateServiceTests.cs
```

### State Transition Examples

**Valid Transitions:**
- `Initiated` → `Process` → `Processing`
- `Processing` → `Complete` → `Succeeded`
- `Succeeded` → `Refund` → `Refunded`
- `Succeeded` → `PartialRefund` → `PartiallyRefunded` → `Refund` → `Refunded`

**Invalid Transitions (throw exceptions):**
- `Pending` → `Complete` ❌ (must go through Processing first)
- `Succeeded` → `Process` ❌ (already completed)
- `Failed` → `Refund` ❌ (cannot refund failed payments)
- `Refunded` → `Refund` ❌ (already refunded)

## Result Pattern Instead of Exceptions (MEDIUM Priority Improvement)

The Payment Microservice implements the **Result Pattern** for functional error handling, replacing exceptions for control flow with explicit error handling.

### Features

- ✅ **Result<T> Pattern**: Functional error handling without exceptions
- ✅ **Domain Error Codes**: Standardized error codes (PAYMENT_NOT_FOUND, CANNOT_REFUND_NON_COMPLETED_PAYMENT, etc.)
- ✅ **Automatic HTTP Mapping**: Errors automatically mapped to appropriate HTTP status codes
- ✅ **Type-Safe Error Handling**: Compile-time safety for error handling
- ✅ **Explicit Error Handling**: Clear error paths in code

### Domain Error Codes

Standardized error codes in `Payment.Domain.Common.ErrorCodes`:

```csharp
// Payment not found errors (404)
ErrorCodes.PaymentNotFound
ErrorCodes.PaymentByOrderIdNotFound

// Validation errors (400)
ErrorCodes.InvalidAmount
ErrorCodes.InvalidCurrency
ErrorCodes.PaymentAlreadyCompleted
ErrorCodes.CannotRefundNonCompletedPayment

// Idempotency errors (409)
ErrorCodes.IdempotencyKeyMismatch

// Provider errors (502)
ErrorCodes.ProviderError
ErrorCodes.ProviderTimeout
```

### Usage in Handlers

Handlers return `Result<T>` instead of throwing exceptions:

```csharp
public async Task<Result<PaymentDto>> Handle(
    GetPaymentByIdQuery request, 
    CancellationToken cancellationToken)
{
    var payment = await _unitOfWork.Payments.GetByIdAsync(request.PaymentId, cancellationToken);
    if (payment == null)
    {
        return Result<PaymentDto>.Failure(
            ErrorCodes.PaymentNotFound, 
            $"Payment with ID {request.PaymentId} not found");
    }
    
    return Result<PaymentDto>.Success(payment.ToDto());
}
```

### Usage in Controllers

Controllers automatically map Result<T> to HTTP status codes:

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<PaymentDto>> GetPaymentById(Guid id, CancellationToken cancellationToken)
{
    var query = new GetPaymentByIdQuery(id);
    var result = await _mediator.Send(query, cancellationToken);
    return result.ToActionResult(); // Automatically maps to 200, 404, 500, etc.
}
```

### Error Mapping

Errors are automatically mapped to HTTP status codes:

- **404 Not Found**: `PAYMENT_NOT_FOUND`, `PAYMENT_BY_ORDER_ID_NOT_FOUND`
- **400 Bad Request**: Validation errors, business logic errors
- **409 Conflict**: `IDEMPOTENCY_KEY_MISMATCH`
- **502 Bad Gateway**: Provider errors
- **500 Internal Server Error**: Unknown errors

### Benefits

- ✅ **No Exception Overhead**: Exceptions are only for truly exceptional cases
- ✅ **Explicit Error Handling**: Clear error paths in code
- ✅ **Type Safety**: Compile-time safety for error handling
- ✅ **Better Performance**: No stack unwinding for expected errors
- ✅ **Functional Style**: Follows functional programming principles

### Testing

Comprehensive unit tests ensure Result pattern works correctly:

```bash
dotnet test tests/Payment.Application.Tests/Handlers/GetPaymentByIdQueryHandlerTests.cs
dotnet test tests/Payment.Domain.Tests/Common/ResultTests.cs
dotnet test tests/Payment.API.Tests/Extensions/ErrorMappingExtensionsTests.cs
```

## Feature Flags (MEDIUM Priority Improvement)

The Payment Microservice implements **Feature Flags** using **Microsoft.FeatureManagement** to enable/disable features without code deployment, supporting gradual rollouts, A/B testing, and safe feature toggling.

### Features

- ✅ **Microsoft.FeatureManagement Integration**: Industry-standard feature flag library
- ✅ **Configuration-Based Flags**: Feature flags defined in `appsettings.json`
- ✅ **Percentage-Based Rollouts**: Gradual rollout support for new features
- ✅ **Controller-Level Gates**: `[FeatureGate]` attribute for endpoint-level control
- ✅ **Handler-Level Checks**: Programmatic feature checks in business logic
- ✅ **Provider Feature Flags**: Control access to new payment providers
- ✅ **Split Payment Feature Flag**: Enable/disable split payment functionality

### Configuration

Feature flags are configured in `appsettings.json`:

```json
{
  "FeatureManagement": {
    "NewPaymentProvider": false,
    "SplitPayments": true,
    "RefundSupport": true,
    "FraudDetection": {
      "EnabledFor": [
        {
          "Name": "Percentage",
          "Parameters": {
            "Value": 50
          }
        }
      ]
    },
    "AdvancedReporting": true,
    "MultiCurrencySettlement": false,
    "ThreeDSecure": false
  }
}
```

### Usage in Controllers

Use `[FeatureGate]` attribute to protect entire endpoints:

```csharp
[HttpPost("{id}/refund")]
[FeatureGate("RefundSupport")]
public async Task<ActionResult<PaymentDto>> RefundPayment(...)
{
    // Only accessible if RefundSupport feature is enabled
}
```

### Usage in Handlers

Check feature flags programmatically in business logic:

```csharp
public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
{
    // Check if fraud detection feature is enabled
    if (await _featureManager.IsEnabledAsync("FraudDetection"))
    {
        await _fraudDetectionService.CheckAsync(request);
    }
    
    // ... rest of logic
}
```

### Usage in Services

Feature flags are checked in `PaymentOrchestrator` for split payments:

```csharp
var splitPaymentsEnabled = await _featureManager.IsEnabledAsync("SplitPayments", cancellationToken);

if (!splitPaymentsEnabled && (request.SplitRule != null || request.SystemFeePercent.HasValue))
{
    throw new InvalidOperationException(
        "Split payments feature is currently disabled.");
}
```

### New Payment Provider Feature Flag

New/experimental payment providers require the `NewPaymentProvider` feature flag:

**New Providers** (require feature flag):
- Checkout
- Verifone
- Paytabs
- Tap

**Established Providers** (no feature flag required):
- ZainCash
- FIB
- Stripe
- Telr
- AsiaHawala
- Square
- Helcim
- Amazon Payment Services

### Benefits

- ✅ **Zero-Downtime Feature Toggles**: Enable/disable features without deployment
- ✅ **Gradual Rollouts**: Test features with a percentage of users
- ✅ **A/B Testing**: Compare feature variations
- ✅ **Risk Mitigation**: Quickly disable problematic features
- ✅ **Provider Control**: Control access to new/experimental providers
- ✅ **Configuration-Driven**: No code changes needed to toggle features

### Testing

Comprehensive unit tests ensure feature flags work correctly:

```bash
dotnet test tests/Payment.Application.Tests/Handlers/FeatureFlagsTests.cs
dotnet test tests/Payment.Application.Tests/Services/PaymentOrchestratorFeatureFlagsTests.cs
dotnet test tests/Payment.Application.Tests/Services/PaymentProviderFactoryFeatureFlagsTests.cs
```

## See Also

- [Kubernetes Deployment](Kubernetes_Deployment.md)
- [Observability & Monitoring](Observability.md)
- [Payment Microservice](../02-Payment/Payment_Microservice.md)

