---
title: System Architecture
version: 1.0
last_updated: 2025-11-11
category: Architecture
tags:
  - architecture
  - clean-architecture
  - solid
  - layers
  - cqrs
summary: >
  Complete system architecture documentation covering Clean Architecture layers,
  dependency flow, and layer-by-layer implementation details.
related_docs:
  - Authentication_Flow.md
  - ../02-Payment/Payment_Microservice.md
ai_context_priority: high
---

# 🏗️ Architecture Overview

```
┌──────────────────────────────────────────┐
│ Presentation Layer → Payment.API          │
│   • Controllers (PaymentsController)      │
│   • JWT Authentication, Swagger, Health   │
│   • Middleware, CORS, Routing             │
├──────────────────────────────────────────┤
│ Application Layer → Payment.Application   │
│   • Commands & Queries (CQRS/MediatR)      │
│   • Handlers (Use Cases)                  │
│   • DTOs, Validators (FluentValidation)   │
│   • Services (Orchestrator, Factory, Split)│
│   • Mappings (Entity ↔ DTO)               │
├──────────────────────────────────────────┤
│ Domain Layer → Payment.Domain             │
│   • Entities (Payment)                    │
│   • Value Objects (Amount, Currency, etc.) │
│   • Domain Events (PaymentCompleted, etc.)│
│   • Interfaces (IPaymentProvider, etc.)   │
│   • Enums (PaymentStatus)                 │
├──────────────────────────────────────────┤
│ Infrastructure Layer → Payment.Infrastructure │
│   • EF Core Persistence (PostgreSQL)      │
│   • Repositories (PaymentRepository)      │
│   • Unit of Work Pattern                  │
│   • Payment Providers (12 implementations)│
│   • Logging (Serilog)                     │
└──────────────────────────────────────────┘
```

## Dependency Flow

- **Presentation** → **Application** → **Domain** ← **Infrastructure**
- Inner layers have **no dependencies** on outer layers
- **Domain** is the **core** with no external dependencies
- **Infrastructure** implements **Domain interfaces**

## 📚 Layer-by-Layer Documentation

### 🎯 Domain Layer (`Payment.Domain`)

The **core business logic** with no external dependencies. Contains entities, value objects, domain events, and interfaces.

#### **Entities**

```7:106:src/Payment.Domain/Entities/Payment.cs
public class Payment : Entity
{
    private Payment() { } // EF Core

    public Payment(
        PaymentId id,
        Amount amount,
        Currency currency,
        PaymentMethod paymentMethod,
        PaymentProvider provider,
        string merchantId,
        string orderId,
        SplitPayment? splitPayment = null,
        Dictionary<string, string>? metadata = null,
        PaymentStatus status = PaymentStatus.Pending)
    {
        Id = id;
        Amount = amount;
        Currency = currency;
        PaymentMethod = paymentMethod;
        Provider = provider;
        MerchantId = merchantId;
        OrderId = orderId;
        SplitPayment = splitPayment;
        Metadata = metadata ?? new Dictionary<string, string>();
        Status = status;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public PaymentId Id { get; private set; }
    public Amount Amount { get; private set; }
    public Currency Currency { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public PaymentProvider Provider { get; private set; }
    public string MerchantId { get; private set; } = string.Empty;
    public string OrderId { get; private set; } = string.Empty;
    public SplitPayment? SplitPayment { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();
    public PaymentStatus Status { get; private set; }
    public string? TransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Process(string transactionId)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot process payment with status {Status}");
        }

        TransactionId = transactionId;
        Status = PaymentStatus.Processing;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentProcessingEvent(Id.Value, OrderId));
    }

    public void Complete()
    {
        if (Status != PaymentStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot complete payment with status {Status}");
        }

        Status = PaymentStatus.Completed;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentCompletedEvent(Id.Value, OrderId, Amount.Value, Currency.Code));
    }

    public void Fail(string reason)
    {
        if (Status == PaymentStatus.Completed)
        {
            throw new InvalidOperationException("Cannot fail a completed payment");
        }

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentFailedEvent(Id.Value, OrderId, reason));
    }

    public void Refund(string refundTransactionId)
    {
        if (Status != PaymentStatus.Completed)
        {
            throw new InvalidOperationException("Can only refund completed payments");
        }

        Status = PaymentStatus.Refunded;
        TransactionId = refundTransactionId;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentRefundedEvent(Id.Value, OrderId, Amount.Value, Currency.Code));
    }
}
```

#### **Value Objects**

Immutable, validated value objects ensure data integrity:

- **`Amount`**: Validates amount > 0
- **`Currency`**: Validates 3-letter ISO code (USD, EUR, GBP, etc.)
- **`PaymentMethod`**: CreditCard, DebitCard, PayPal, BankTransfer, Crypto, Wallet, Card, Cash
- **`PaymentProvider`**: ZainCash, AsiaHawala, Stripe, FIB, Square, Helcim, AmazonPaymentServices, Telr, Checkout, Verifone, Paytabs, Tap
- **`PaymentId`**: Strongly-typed GUID wrapper
- **`SplitPayment`**: Calculates system/owner shares with fee percentage

```3:29:src/Payment.Domain/ValueObjects/SplitPayment.cs
public sealed record SplitPayment(
    decimal SystemShare,
    decimal OwnerShare,
    decimal SystemFeePercent)
{
    public static SplitPayment Calculate(decimal totalAmount, decimal systemFeePercent)
    {
        if (totalAmount <= 0)
        {
            throw new ArgumentException("Total amount must be greater than zero", nameof(totalAmount));
        }

        if (systemFeePercent < 0 || systemFeePercent > 100)
        {
            throw new ArgumentException("System fee percent must be between 0 and 100", nameof(systemFeePercent));
        }

        var systemShare = Math.Round(totalAmount * systemFeePercent / 100, 2);
        var ownerShare = Math.Round(totalAmount - systemShare, 2);

        return new SplitPayment(systemShare, ownerShare, systemFeePercent);
    }

    public decimal TotalAmount => SystemShare + OwnerShare;
}
```

#### **Domain Events**

Domain events for event-driven architecture:

- `PaymentProcessingEvent`: Payment started processing
- `PaymentCompletedEvent`: Payment successfully completed
- `PaymentFailedEvent`: Payment failed with reason
- `PaymentRefundedEvent`: Payment refunded

```3:11:src/Payment.Domain/Events/PaymentCompletedEvent.cs
public sealed record PaymentCompletedEvent(
    Guid PaymentId,
    string OrderId,
    decimal Amount,
    string Currency) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
```

#### **Interfaces**

Domain interfaces define contracts implemented by Infrastructure. **Follows Interface Segregation Principle (ISP)** - interfaces are focused and cohesive:

- `IPaymentProvider`: Payment processing contract
- `IPaymentCallbackProvider`: Callback/webhook verification contract (separated for ISP compliance)
- `IPaymentRepository`: Payment persistence contract
- `IUnitOfWork`: Transaction management contract

```5:25:src/Payment.Domain/Interfaces/IPaymentProvider.cs
public interface IPaymentProvider
{
    string ProviderName { get; }
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}

public sealed record PaymentRequest(
    Amount Amount,
    Currency Currency,
    string MerchantId,
    string OrderId,
    SplitPayment? SplitPayment,
    Dictionary<string, string>? Metadata);

public sealed record PaymentResult(
    bool Success,
    string? TransactionId,
    string? FailureReason,
    Dictionary<string, string>? ProviderMetadata);
```

**IPaymentCallbackProvider** (Interface Segregation Principle):
```1:15:src/Payment.Domain/Interfaces/IPaymentCallbackProvider.cs
namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for payment providers that support callback/webhook verification.
/// Follows Interface Segregation Principle - separates callback concerns from payment processing.
/// </summary>
public interface IPaymentCallbackProvider
{
    /// <summary>
    /// Verifies a payment callback/webhook from the provider.
    /// </summary>
    /// <param name="callbackData">Provider-specific callback data (token, payment ID, order ID, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment result with verification status</returns>
    Task<PaymentResult> VerifyCallbackAsync(
        Dictionary<string, string> callbackData,
        CancellationToken cancellationToken = default);
}
```

### 🎯 Application Layer (`Payment.Application`)

Implements **use cases** using **CQRS pattern** with **MediatR**. Contains commands, queries, handlers, DTOs, validators, and orchestration services.

#### **CQRS Commands & Queries**

**Commands** (Write operations):
- `CreatePaymentCommand`: Initiate a new payment
- `ProcessPaymentCommand`: Mark payment as processing
- `CompletePaymentCommand`: Mark payment as completed
- `FailPaymentCommand`: Mark payment as failed
- `RefundPaymentCommand`: Refund a completed payment
- `HandlePaymentCallbackCommand`: Handle payment provider callbacks/webhooks (NEW - follows CQRS pattern)

**Queries** (Read operations):
- `GetPaymentByIdQuery`: Get payment by ID
- `GetPaymentByOrderIdQuery`: Get payment by order ID
- `GetPaymentsByMerchantQuery`: Get all payments for a merchant

```6:21:src/Payment.Application/Commands/CreatePaymentCommand.cs
public sealed record CreatePaymentCommand(
    Guid RequestId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Provider,
    string MerchantId,
    string OrderId,
    string ProjectCode,
    decimal? SystemFeePercent = null,
    SplitRuleDto? SplitRule = null,
    Dictionary<string, string>? Metadata = null,
    string? CallbackUrl = null,
    string? CustomerEmail = null,
    string? CustomerPhone = null) : IRequest<PaymentDto>;
```

#### **Command Handlers (Use Cases)**

Handlers implement business logic for each command/query:

```8:37:src/Payment.Application/Handlers/CreatePaymentCommandHandler.cs
public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    private readonly IPaymentOrchestrator _orchestrator;

    public CreatePaymentCommandHandler(IPaymentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var createPaymentDto = new CreatePaymentDto(
            request.RequestId,
            request.Amount,
            request.Currency,
            request.PaymentMethod,
            request.Provider,
            request.MerchantId,
            request.OrderId,
            request.ProjectCode,
            request.SystemFeePercent,
            request.SplitRule,
            request.Metadata,
            request.CallbackUrl,
            request.CustomerEmail,
            request.CustomerPhone);

        return await _orchestrator.ProcessPaymentAsync(createPaymentDto, cancellationToken);
    }
}
```

#### **Payment Orchestrator**

Coordinates the payment processing workflow, delegating to specialized services:

```50:122:src/Payment.Application/Services/PaymentOrchestrator.cs
    public async Task<PaymentDto> ProcessPaymentAsync(CreatePaymentDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing payment for order {OrderId} with provider {Provider}, requestId={RequestId}, projectCode={ProjectCode}", 
            request.OrderId, request.Provider, request.RequestId, request.ProjectCode);

        // Step 1: Check idempotency (delegated to IIdempotencyService)
        var existingPayment = await _idempotencyService.CheckExistingPaymentAsync(request, cancellationToken);
        if (existingPayment != null)
        {
            _logger.LogInformation("Payment already exists for order {OrderId}, returning existing payment", request.OrderId);
            return existingPayment.ToDto();
        }

        // Step 2: Enrich metadata (delegated to IMetadataEnrichmentService)
        var metadata = _metadataEnrichmentService.EnrichMetadata(request, request.Metadata);

        // Step 3: Calculate split payment (delegated to ISplitPaymentService)
        SplitPayment? splitPayment = null;
        if (request.SplitRule != null)
        {
            var (split, splitDetails) = _splitPaymentService.CalculateMultiAccountSplit(request.Amount, request.SplitRule);
            splitPayment = split;
            
            // Store detailed split information in metadata
            var splitDetailsJson = JsonSerializer.Serialize(splitDetails);
            metadata["split_details"] = splitDetailsJson;
            
            _logger.LogInformation("Multi-account split payment calculated: System={SystemShare}, Owner={OwnerShare}, Accounts={AccountCount}",
                splitPayment.SystemShare, splitPayment.OwnerShare, request.SplitRule.Accounts.Count);
        }
        else if (request.SystemFeePercent.HasValue && request.SystemFeePercent.Value > 0)
        {
            splitPayment = _splitPaymentService.CalculateSplit(request.Amount, request.SystemFeePercent.Value);
            _logger.LogInformation("Simple split payment calculated: System={SystemShare}, Owner={OwnerShare}, Fee={FeePercent}%",
                splitPayment.SystemShare, splitPayment.OwnerShare, splitPayment.SystemFeePercent);
        }

        // Step 4: Create payment entity (delegated to IPaymentFactory)
        var payment = _paymentFactory.CreatePayment(request, splitPayment, metadata);

        // Step 5: Persist payment
        await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Step 6: Get payment provider (delegated to IPaymentProviderFactory)
        var provider = _providerFactory.Create(request.Provider);

        // Step 7: Process payment through provider (delegated to IPaymentProcessingService)
        try
        {
            var result = await _paymentProcessingService.ProcessPaymentAsync(payment, provider, cancellationToken);

            // Step 8: Update payment status (delegated to IPaymentStatusUpdater)
            _paymentStatusUpdater.UpdatePaymentStatus(payment, result);

            await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return payment.ToDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment {PaymentId} with provider {Provider}",
                payment.Id.Value, request.Provider);
            
            payment.Fail($"Provider error: {ex.Message}");
            await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            throw;
        }
    }
```

#### **Application Services**

Specialized services following **Single Responsibility Principle**:

1. **`PaymentProviderFactory`**: Creates provider instances (Factory Pattern)
2. **`SplitPaymentService`**: Calculates split payments (simple or multi-account)
3. **`IdempotencyService`**: Ensures idempotent operations
4. **`MetadataEnrichmentService`**: Enriches payment metadata
5. **`PaymentFactory`**: Creates Payment domain entities
6. **`PaymentProcessingService`**: Processes payments through providers
7. **`PaymentStatusUpdater`**: Updates payment status based on provider results

```11:49:src/Payment.Application/Services/PaymentProviderFactory.cs
public class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PaymentProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPaymentProvider Create(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
        }

        var normalizedName = providerName.Trim();
        
        // Get all registered payment providers and find the one matching the name
        var providers = _serviceProvider.GetServices<IPaymentProvider>();
        var provider = providers.FirstOrDefault(p => 
            string.Equals(p.ProviderName, normalizedName, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            throw new NotSupportedException(
                $"Payment provider '{providerName}' is not supported or not registered. " +
                $"Available providers: {string.Join(", ", GetAvailableProviders())}");
        }

        return provider;
    }

    public IEnumerable<string> GetAvailableProviders()
    {
        var providers = _serviceProvider.GetServices<IPaymentProvider>();
        return providers.Select(p => p.ProviderName);
    }
}
```

#### **DTOs**

Data Transfer Objects for API contracts:

- `CreatePaymentDto`: Payment creation request
- `PaymentDto`: Payment response
- `SplitPaymentDto`: Split payment information
- `SplitRuleDto`: Multi-account split rule
- `SplitAccountDto`: Individual account in split rule
- `CallbackRequestDtos`: Request DTOs for payment callbacks (moved from controller to Application layer)

```7:23:src/Payment.Application/DTOs/CreatePaymentDto.cs
public sealed record CreatePaymentDto(
    Guid RequestId,                       // For idempotency and traceability
    decimal Amount,
    string Currency,
    string PaymentMethod,                 // e.g. "Wallet", "Card", "Cash"
    string Provider,                      // e.g. "ZainCash", "Stripe"
    string MerchantId,                    // Owning merchant (or service owner)
    string OrderId,                       // External order reference
    string ProjectCode,                   // Identifies the project or tenant
    decimal? SystemFeePercent = null,     // Optional override; if null, fetched from Config Service
    SplitRuleDto? SplitRule = null,       // Optional explicit rule (multi-account split)
    Dictionary<string, string>? Metadata = null,
    string? CallbackUrl = null,           // Optional: Provider webhook for async confirmation
    string? CustomerEmail = null,         // For receipts / provider requirements
    string? CustomerPhone = null          // For wallet-based providers
);
```

### 🎯 Infrastructure Layer (`Payment.Infrastructure`)

Implements persistence, external integrations, and payment providers.

#### **Payment Providers (Strategy Pattern)**

13 payment provider implementations:

1. **ZainCashPaymentProvider** - Middle East wallet payments
2. **AsiaHawalaPaymentProvider** - Hawala payment system
3. **StripePaymentProvider** - Global card payments
4. **FibPaymentProvider** - FIB bank integration
5. **SquarePaymentProvider** - Square payment processing
6. **HelcimPaymentProvider** - Helcim payment gateway
7. **AmazonPaymentProvider** - Amazon Payment Services
8. **TelrPaymentProvider** - Telr payment gateway
9. **CheckoutPaymentProvider** - Checkout.com integration
10. **VerifonePaymentProvider** - Verifone payment gateway
11. **PaytabsPaymentProvider** - Paytabs payment gateway
12. **TapPaymentProvider** - Tap Payments integration
13. **TapToPayPaymentProvider** - Tap-to-Pay NFC/HCE contactless payments (Apple Pay, Google Pay, Tap SDK)

Each provider implements `IPaymentProvider` interface, enabling **Open/Closed Principle** - new providers can be added without modifying existing code.

#### **Persistence**

- **EF Core** with **PostgreSQL**
- **Repository Pattern** with `PaymentRepository`
- **Unit of Work Pattern** for transaction management
- **Entity Configurations** for database mapping

### 🎯 Presentation Layer (`Payment.API`)

RESTful API with controllers, authentication, and middleware.

#### **PaymentsController**

RESTful endpoints for payment operations. **Follows Clean Architecture** - thin controller that delegates to Application layer via MediatR. **No direct dependencies on Infrastructure layer**.

```17:33:src/Payment.API/Controllers/PaymentsController.cs
/// <summary>
/// Payments API Controller.
/// Follows Clean Architecture - thin controller that delegates to Application layer via MediatR.
/// No direct dependencies on Infrastructure layer.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IPaymentProviderFactory _providerFactory;

    public PaymentsController(
        IMediator mediator, 
        ILogger<PaymentsController> logger,
        IPaymentProviderFactory providerFactory)
    {
        _mediator = mediator;
        _logger = logger;
        _providerFactory = providerFactory;
    }

    /// <summary>
    /// Gets available payment providers
    /// </summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public ActionResult<IEnumerable<string>> GetAvailableProviders()
    {
        var providers = _providerFactory.GetAvailableProviders();
        return Ok(providers);
    }

    /// <summary>
    /// Creates a new payment
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentDto>> CreatePayment([FromBody] CreatePaymentDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating payment for order {OrderId}, requestId={RequestId}, projectCode={ProjectCode}", 
            dto.OrderId, dto.RequestId, dto.ProjectCode);

        var command = new CreatePaymentCommand(
            dto.RequestId,
            dto.Amount,
            dto.Currency,
            dto.PaymentMethod,
            dto.Provider,
            dto.MerchantId,
            dto.OrderId,
            dto.ProjectCode,
            dto.SystemFeePercent,
            dto.SplitRule,
            dto.Metadata,
            dto.CallbackUrl,
            dto.CustomerEmail,
            dto.CustomerPhone);

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetPaymentById), new { id = result.Id }, result);
    }
```

#### **Program.cs Configuration**

```23:92:src/Payment.API/Program.cs
// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payment Microservice API",
        Version = "v1",
        Description = "A production-ready Payment microservice built with Clean Architecture"
    });

    // JWT Authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// JWT Authentication - External Identity Microservice
// Validates tokens against the Identity Microservice's JWKS endpoint
var authority = builder.Configuration["Auth:Authority"] 
    ?? throw new InvalidOperationException("Auth:Authority not configured");
var audience = builder.Configuration["Auth:Audience"] 
    ?? throw new InvalidOperationException("Auth:Audience not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = authority;
    options.Audience = audience;
    options.RequireHttpsMetadata = true;
    // Token validation is performed against the Identity Microservice's JWKS endpoint
    // No local secret key is required - tokens are validated using public keys from the authority
});

// Authorization Policies for fine-grained control
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PaymentsWrite", policy =>
        policy.RequireClaim("scope", "payment.write"));
    
    options.AddPolicy("PaymentsRead", policy =>
        policy.RequireClaim("scope", "payment.read"));
    
    options.AddPolicy("PaymentsAdmin", policy =>
        policy.RequireClaim("scope", "payment.admin"));
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>();

// Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

## See Also

- [Authentication Flow](Authentication_Flow.md)
- [Payment Microservice](../02-Payment/Payment_Microservice.md)
- [Extension Guide](../04-Guidelines/Extension_Guide.md)

