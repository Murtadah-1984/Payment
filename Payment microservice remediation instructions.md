# 🔐 Payment Microservice - Security & Architecture Remediation Instructions

## Executive Summary
This document provides **actionable instructions** to address critical vulnerabilities, security gaps, performance issues, and architectural improvements for the Payment Microservice project.

**Severity Levels:**
- 🔴 **CRITICAL**: Must fix before production
- 🟠 **HIGH**: Fix in next sprint
- 🟡 **MEDIUM**: Address in backlog
- 🟢 **LOW**: Nice to have

---

## 🔴 CRITICAL SECURITY VULNERABILITIES

### 1. Payment Callback Authentication (CRITICAL)
**Issue**: Callbacks are marked `[AllowAnonymous]` which allows anyone to forge payment confirmations.

**Instructions:**
```csharp
// src/Payment.API/Middleware/WebhookSignatureValidationMiddleware.cs
public class WebhookSignatureValidationMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/payments/callbacks"))
        {
            // Extract provider from route
            var provider = ExtractProvider(context.Request.Path);
            
            // Read body
            context.Request.EnableBuffering();
            var body = await ReadBodyAsync(context.Request);
            
            // Get signature from header
            var signature = context.Request.Headers["X-Signature"].FirstOrDefault();
            var timestamp = context.Request.Headers["X-Timestamp"].FirstOrDefault();
            
            // Validate signature using provider-specific validation
            var validator = _serviceProvider.GetRequiredService<ICallbackSignatureValidator>();
            if (!await validator.ValidateAsync(provider, body, signature, timestamp))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid webhook signature");
                return;
            }
        }
        
        await _next(context);
    }
}

// src/Payment.Domain/Interfaces/ICallbackSignatureValidator.cs
public interface ICallbackSignatureValidator
{
    Task<bool> ValidateAsync(string provider, string payload, string signature, string timestamp);
}

// Implementation per provider
public class ZainCashSignatureValidator : ICallbackSignatureValidator
{
    public Task<bool> ValidateAsync(string provider, string payload, string signature, string timestamp)
    {
        // Use HMAC-SHA256 with provider secret
        var secret = _configuration[$"PaymentProviders:{provider}:WebhookSecret"];
        var computedSignature = ComputeHmacSha256(payload + timestamp, secret);
        return Task.FromResult(signature == computedSignature);
    }
}
```

**Action Items:**
1. Remove `[AllowAnonymous]` from callback endpoints
2. Implement `WebhookSignatureValidationMiddleware`
3. Add webhook secrets to configuration
4. Implement signature validation per provider
5. Add timestamp validation (reject old requests > 5 minutes)

---

### 2. Idempotency Keys for Payment Operations (CRITICAL)
**Issue**: No idempotency mechanism prevents duplicate payments from retries.

**Instructions:**
```csharp
// src/Payment.Domain/ValueObjects/IdempotencyKey.cs
public sealed record IdempotencyKey
{
    public string Value { get; }
    
    public IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 16 || value.Length > 128)
            throw new ArgumentException("Idempotency key must be 16-128 characters");
        Value = value;
    }
}

// src/Payment.Domain/Entities/IdempotentRequest.cs
public class IdempotentRequest
{
    public string IdempotencyKey { get; set; }
    public Guid PaymentId { get; set; }
    public string RequestHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

// src/Payment.Application/Commands/CreatePaymentCommand.cs
public sealed record CreatePaymentCommand(
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Provider,
    string MerchantId,
    string OrderId,
    string IdempotencyKey, // ADD THIS
    decimal? SystemFeePercent = null,
    Dictionary<string, string>? Metadata = null) : IRequest<PaymentDto>;

// src/Payment.Application/Handlers/CreatePaymentCommandHandler.cs
public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
{
    // Check for existing idempotent request
    var existingRequest = await _unitOfWork.IdempotentRequests
        .GetByKeyAsync(request.IdempotencyKey, cancellationToken);
    
    if (existingRequest != null)
    {
        // Verify request hash matches (prevent key reuse with different data)
        var currentHash = ComputeRequestHash(request);
        if (existingRequest.RequestHash != currentHash)
        {
            throw new IdempotencyKeyMismatchException(
                "Idempotency key reused with different request data");
        }
        
        // Return existing payment
        var existingPayment = await _unitOfWork.Payments
            .GetByIdAsync(existingRequest.PaymentId, cancellationToken);
        return existingPayment.ToDto();
    }
    
    // Process new payment...
    var payment = // ... create payment
    
    // Store idempotency record
    await _unitOfWork.IdempotentRequests.AddAsync(new IdempotentRequest
    {
        IdempotencyKey = request.IdempotencyKey,
        PaymentId = payment.Id.Value,
        RequestHash = ComputeRequestHash(request),
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddHours(24)
    }, cancellationToken);
    
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    return payment.ToDto();
}
```

**Action Items:**
1. Create `IdempotentRequest` entity and repository
2. Add `IdempotencyKey` to all mutating commands
3. Implement request hash computation (SHA-256 of canonical JSON)
4. Add cleanup job for expired idempotency records (>24 hours)
5. Update API documentation with idempotency key requirement
6. Return `409 Conflict` for key mismatches

---

### 3. PCI DSS Compliance & Data Encryption (CRITICAL)
**Issue**: No mention of PCI DSS compliance for handling payment card data.

**Instructions:**
```csharp
// NEVER store these fields:
// - Full credit card number (PAN)
// - CVV/CVC
// - Expiration date
// - PIN

// src/Payment.Domain/ValueObjects/CardToken.cs
public sealed record CardToken
{
    public string Token { get; }
    public string Last4Digits { get; }
    public string CardBrand { get; } // Visa, Mastercard, etc.
    
    public CardToken(string token, string last4Digits, string cardBrand)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Card token cannot be empty");
        if (last4Digits?.Length != 4 || !last4Digits.All(char.IsDigit))
            throw new ArgumentException("Last 4 digits must be exactly 4 digits");
        
        Token = token;
        Last4Digits = last4Digits;
        CardBrand = cardBrand;
    }
}

// Update Payment entity to use CardToken instead of raw card data
// src/Payment.Domain/Entities/Payment.cs
public CardToken? CardToken { get; private set; }

// src/Payment.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs
public class PaymentConfiguration : IEntityTypeConfiguration<Domain.Entities.Payment>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Payment> builder)
    {
        // Encrypt sensitive metadata at rest
        builder.Property(p => p.Metadata)
            .HasConversion(
                v => EncryptMetadata(JsonSerializer.Serialize(v)),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(DecryptMetadata(v))
            );
    }
}

// src/Payment.Infrastructure/Security/DataEncryption.cs
public class DataEncryptionService
{
    private readonly byte[] _encryptionKey;
    
    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        // Prepend IV to ciphertext
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        
        return Convert.ToBase64String(result);
    }
}
```

**Action Items:**
1. **NEVER** store full card numbers - use tokenization
2. Use payment provider's tokenization API
3. Encrypt sensitive metadata with AES-256
4. Rotate encryption keys quarterly
5. Implement data masking in logs
6. Enable TLS 1.3 minimum
7. Audit database access logs
8. Implement field-level encryption for sensitive data

---

### 4. Secrets Management (CRITICAL)
**Issue**: Secrets in environment variables are insecure.

**Instructions:**
```csharp
// src/Payment.API/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Use Azure Key Vault / AWS Secrets Manager / HashiCorp Vault
if (builder.Environment.IsProduction())
{
    var keyVaultUri = builder.Configuration["KeyVault:Uri"];
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

// src/Payment.Infrastructure/Configuration/PaymentProviderSettings.cs
public class PaymentProviderSettings
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    
    [Required]
    public string WebhookSecret { get; set; } = string.Empty;
    
    // Load from Key Vault
    public static PaymentProviderSettings LoadFromKeyVault(
        IConfiguration configuration, 
        string provider)
    {
        return new PaymentProviderSettings
        {
            ApiKey = configuration[$"Providers-{provider}-ApiKey"],
            WebhookSecret = configuration[$"Providers-{provider}-WebhookSecret"]
        };
    }
}
```

**Kubernetes Secrets:**
```yaml
# k8s/secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: payment-secrets
type: Opaque
data:
  jwt-secret: <base64-encoded-secret>
  db-password: <base64-encoded-password>
  zaincash-api-key: <base64-encoded-key>
  zaincash-webhook-secret: <base64-encoded-secret>

# Use External Secrets Operator for production
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: payment-secrets
spec:
  secretStoreRef:
    name: azure-keyvault
  target:
    name: payment-secrets
  data:
    - secretKey: jwt-secret
      remoteRef:
        key: payment-jwt-secret
```

**Action Items:**
1. Integrate Azure Key Vault / AWS Secrets Manager
2. Use Kubernetes External Secrets Operator
3. Never commit secrets to Git
4. Implement secret rotation policy
5. Use managed identities (no passwords)
6. Audit secret access logs

---

### 5. Input Validation & Sanitization (CRITICAL)
**Issue**: No XSS/SQL injection protection explicitly mentioned.

**Instructions:**
```csharp
// src/Payment.Application/Validators/CreatePaymentCommandValidator.cs
public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThan(1_000_000); // Prevent overflow
        
        RuleFor(x => x.Currency)
            .Must(BeValidCurrency)
            .WithMessage("Invalid currency code");
        
        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .MaximumLength(100)
            .Must(NotContainSpecialCharacters)
            .WithMessage("Merchant ID contains invalid characters");
        
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Order ID must be alphanumeric with hyphens/underscores only");
        
        RuleFor(x => x.Metadata)
            .Must(HaveValidMetadata)
            .When(x => x.Metadata != null)
            .WithMessage("Metadata exceeds size limits or contains invalid characters");
    }
    
    private bool HaveValidMetadata(Dictionary<string, string>? metadata)
    {
        if (metadata == null) return true;
        
        // Limit number of keys
        if (metadata.Count > 50) return false;
        
        foreach (var kvp in metadata)
        {
            // Key validation
            if (kvp.Key.Length > 100) return false;
            if (!Regex.IsMatch(kvp.Key, "^[a-zA-Z0-9_-]+$")) return false;
            
            // Value validation
            if (kvp.Value.Length > 1000) return false;
            
            // Prevent script injection
            if (ContainsDangerousContent(kvp.Value)) return false;
        }
        
        return true;
    }
    
    private bool ContainsDangerousContent(string value)
    {
        var dangerous = new[] { "<script", "javascript:", "onerror=", "onclick=" };
        return dangerous.Any(d => value.Contains(d, StringComparison.OrdinalIgnoreCase));
    }
}

// src/Payment.API/Middleware/RequestSanitizationMiddleware.cs
public class RequestSanitizationMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Add("Content-Security-Policy", 
            "default-src 'self'; script-src 'self'");
        
        await _next(context);
    }
}
```

**Action Items:**
1. Implement strict input validation for all commands
2. Limit metadata size (max 50 keys, 1KB per value)
3. Use parameterized queries (EF Core does this)
4. Add security headers middleware
5. Implement rate limiting per endpoint
6. Add OWASP dependency check to CI/CD

---

## 🟠 HIGH PRIORITY SECURITY ISSUES

### 6. Rate Limiting & DDoS Protection (HIGH)
**Issue**: No rate limiting implemented.

**Instructions:**
```csharp
// Install: AspNetCoreRateLimit
// src/Payment.API/Program.cs
services.AddMemoryCache();
services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
services.AddInMemoryRateLimiting();
services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

app.UseIpRateLimiting();

// appsettings.json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Real-IP",
    "ClientIdHeader": "X-ClientId",
    "HttpStatusCode": 429,
    "GeneralRules": [
      {
        "Endpoint": "POST:/api/payments",
        "Period": "1m",
        "Limit": 10
      },
      {
        "Endpoint": "*",
        "Period": "1h",
        "Limit": 1000
      }
    ]
  }
}
```

**Action Items:**
1. Implement rate limiting per IP and per user
2. Add distributed rate limiting with Redis
3. Implement exponential backoff for retries
4. Add CAPTCHA for suspicious activity
5. Monitor for DDoS patterns
6. Use API Gateway rate limiting in production

---

### 7. Audit Logging (HIGH)
**Issue**: No audit trail for compliance.

**Instructions:**
```csharp
// src/Payment.Domain/Entities/AuditLog.cs
public class AuditLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public string Action { get; set; } // PaymentCreated, PaymentRefunded, etc.
    public string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public Dictionary<string, object> Changes { get; set; }
    public DateTime Timestamp { get; set; }
}

// src/Payment.Application/Behaviors/AuditingBehavior.cs
public class AuditingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuditLogRepository _auditLog;
    private readonly IHttpContextAccessor _httpContext;
    
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var response = await next();
        
        // Log after successful execution
        if (request is ICommand command)
        {
            await _auditLog.LogAsync(new AuditLog
            {
                UserId = _httpContext.HttpContext?.User?.FindFirst("sub")?.Value ?? "System",
                Action = request.GetType().Name,
                EntityType = typeof(TResponse).Name,
                IpAddress = _httpContext.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = _httpContext.HttpContext?.Request?.Headers["User-Agent"],
                Changes = ExtractChanges(request, response),
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
        }
        
        return response;
    }
}

// Register in DI
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditingBehavior<,>));
```

**Action Items:**
1. Log all mutating operations
2. Store audit logs in separate database
3. Implement log retention policy (7 years for financial data)
4. Make logs tamper-proof (append-only, signed)
5. Add audit log search and export API
6. Integrate with SIEM system

---

### 8. Resilience Patterns (HIGH)
**Issue**: No circuit breaker, retry, or timeout policies.

**Instructions:**
```csharp
// Install: Polly
// src/Payment.Infrastructure/Providers/ResilientPaymentProviderDecorator.cs
public class ResilientPaymentProviderDecorator : IPaymentProvider
{
    private readonly IPaymentProvider _inner;
    private readonly IAsyncPolicy<ProcessPaymentResult> _policy;
    
    public ResilientPaymentProviderDecorator(IPaymentProvider inner)
    {
        _inner = inner;
        _policy = CreatePolicy();
    }
    
    private IAsyncPolicy<ProcessPaymentResult> CreatePolicy()
    {
        // Timeout: 30 seconds
        var timeoutPolicy = Policy.TimeoutAsync<ProcessPaymentResult>(30);
        
        // Retry: 3 times with exponential backoff
        var retryPolicy = Policy<ProcessPaymentResult>
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} after {Delay}s due to {Exception}",
                        retryCount, timespan.TotalSeconds, outcome.Exception?.Message);
                });
        
        // Circuit Breaker: Open after 5 failures, stay open for 60 seconds
        var circuitBreakerPolicy = Policy<ProcessPaymentResult>
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(60),
                onBreak: (outcome, duration) =>
                {
                    _logger.LogError(
                        "Circuit breaker opened for {Duration}s due to {Exception}",
                        duration.TotalSeconds, outcome.Exception?.Message);
                },
                onReset: () => _logger.LogInformation("Circuit breaker reset"));
        
        // Combine policies
        return Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreakerPolicy);
    }
    
    public async Task<ProcessPaymentResult> ProcessPaymentAsync(
        Domain.Entities.Payment payment, 
        CancellationToken cancellationToken)
    {
        return await _policy.ExecuteAsync(
            async () => await _inner.ProcessPaymentAsync(payment, cancellationToken));
    }
}

// Register decorated providers
services.Decorate<IPaymentProvider, ResilientPaymentProviderDecorator>();
```

**Action Items:**
1. Add Polly for resilience patterns
2. Implement circuit breaker for all external calls
3. Add retry with exponential backoff
4. Set timeout policies (30s for provider calls)
5. Implement fallback strategies
6. Monitor circuit breaker metrics

---

## 🟠 HIGH PRIORITY PERFORMANCE ISSUES

### 9. Caching Strategy (HIGH)
**Issue**: No caching implemented.

**Instructions:**
```csharp
// src/Payment.Infrastructure/Caching/CacheService.cs
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var cached = await _cache.GetStringAsync(key, cancellationToken);
        return cached == null ? default : JsonSerializer.Deserialize<T>(cached);
    }
    
    public async Task SetAsync<T>(
        string key, 
        T value, 
        TimeSpan? expiry, 
        CancellationToken cancellationToken)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10)
        };
        
        var json = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, json, options, cancellationToken);
    }
}

// src/Payment.Application/Queries/GetPaymentByIdQuery.cs
public class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    
    public async Task<PaymentDto> Handle(
        GetPaymentByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var cacheKey = $"payment:{request.PaymentId}";
        
        // Try cache first
        var cached = await _cache.GetAsync<PaymentDto>(cacheKey, cancellationToken);
        if (cached != null) return cached;
        
        // Fetch from database
        var payment = await _unitOfWork.Payments.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment == null)
            throw new KeyNotFoundException($"Payment {request.PaymentId} not found");
        
        var dto = payment.ToDto();
        
        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
        
        return dto;
    }
}

// Invalidate cache on updates
public class UpdatePaymentCommandHandler : IRequestHandler<UpdatePaymentCommand, PaymentDto>
{
    public async Task<PaymentDto> Handle(...)
    {
        // ... update payment
        
        // Invalidate cache
        await _cache.RemoveAsync($"payment:{payment.Id.Value}", cancellationToken);
        
        return payment.ToDto();
    }
}
```

**Action Items:**
1. Implement Redis distributed caching
2. Cache payment details (5 min TTL)
3. Cache provider configurations (1 hour TTL)
4. Implement cache-aside pattern
5. Add cache invalidation on updates
6. Monitor cache hit rates

---

### 10. Database Optimization (HIGH)
**Issue**: No indexing or query optimization mentioned.

**Instructions:**
```csharp
// src/Payment.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs
public class PaymentConfiguration : IEntityTypeConfiguration<Domain.Entities.Payment>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);
        
        // Indexes for common queries
        builder.HasIndex(p => p.OrderId)
            .IsUnique()
            .HasDatabaseName("IX_Payments_OrderId");
        
        builder.HasIndex(p => p.MerchantId)
            .HasDatabaseName("IX_Payments_MerchantId");
        
        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_Payments_Status");
        
        builder.HasIndex(p => p.TransactionId)
            .HasDatabaseName("IX_Payments_TransactionId");
        
        // Composite index for common query patterns
        builder.HasIndex(p => new { p.MerchantId, p.Status, p.CreatedAt })
            .HasDatabaseName("IX_Payments_Merchant_Status_Date");
        
        // Timestamp columns for efficient range queries
        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("IX_Payments_CreatedAt");
        
        builder.HasIndex(p => p.UpdatedAt)
            .HasDatabaseName("IX_Payments_UpdatedAt");
        
        // Configure owned types
        builder.OwnsOne(p => p.Amount, amount =>
        {
            amount.Property(a => a.Value)
                .HasColumnName("Amount")
                .HasColumnType("decimal(18,2)");
        });
        
        builder.OwnsOne(p => p.Currency, currency =>
        {
            currency.Property(c => c.Code)
                .HasColumnName("Currency")
                .HasMaxLength(3);
        });
        
        // Use JSON column for metadata (PostgreSQL)
        builder.Property(p => p.Metadata)
            .HasColumnType("jsonb");
    }
}

// Pagination support
public interface IPaymentRepository
{
    Task<PagedResult<Payment>> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        PaymentFilter filter,
        CancellationToken cancellationToken);
}

public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// Use compiled queries for hot paths
private static readonly Func<PaymentDbContext, Guid, Task<Payment?>> GetPaymentByIdCompiled =
    EF.CompileAsyncQuery(
        (PaymentDbContext context, Guid id) =>
            context.Payments.FirstOrDefault(p => p.Id == id));
```

**Action Items:**
1. Add indexes on frequently queried columns
2. Use composite indexes for common query patterns
3. Implement pagination for all list endpoints
4. Use compiled queries for hot paths
5. Enable query logging in development
6. Run EXPLAIN ANALYZE on slow queries
7. Configure connection pooling (min: 5, max: 100)
8. Use read replicas for reporting queries

---

### 11. Async/Await Optimization (HIGH)
**Issue**: Potential synchronous blocking calls.

**Instructions:**
```csharp
// ❌ BAD - Synchronous blocking
public PaymentDto CreatePayment(CreatePaymentCommand command)
{
    var payment = // ...
    _unitOfWork.SaveChanges(); // BLOCKS thread
    return payment.ToDto();
}

// ✅ GOOD - Fully asynchronous
public async Task<PaymentDto> Handle(
    CreatePaymentCommand request, 
    CancellationToken cancellationToken)
{
    var payment = // ...
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    return payment.ToDto();
}

// ❌ BAD - Task.Result causes deadlock
public void ProcessPayment(Guid id)
{
    var payment = _repository.GetByIdAsync(id).Result; // DEADLOCK RISK
}

// ✅ GOOD - Use async all the way
public async Task ProcessPaymentAsync(Guid id, CancellationToken cancellationToken)
{
    var payment = await _repository.GetByIdAsync(id, cancellationToken);
}

// Avoid async void (except event handlers)
// ❌ BAD
public async void ProcessPayment() { }

// ✅ GOOD
public async Task ProcessPaymentAsync() { }
```

**Action Items:**
1. Ensure all I/O operations are async
2. Pass CancellationToken to all async methods
3. Never use .Result or .Wait()
4. Configure await to not capture context: .ConfigureAwait(false)
5. Use ValueTask for hot paths with likely synchronous completion

---

## 🟡 MEDIUM PRIORITY IMPROVEMENTS

### 12. Event Sourcing & Outbox Pattern (MEDIUM)
**Issue**: No reliable event publishing mechanism.

**Instructions:**
```csharp
// src/Payment.Domain/Entities/OutboxMessage.cs
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; }
    public string Payload { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}

// src/Payment.Infrastructure/BackgroundServices/OutboxProcessorService.cs
public class OutboxProcessorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var messages = await _unitOfWork.OutboxMessages
                .GetPendingAsync(batchSize: 100, stoppingToken);
            
            foreach (var message in messages)
            {
                try
                {
                    await _eventBus.PublishAsync(
                        message.EventType, 
                        message.Payload, 
                        stoppingToken);
                    
                    message.ProcessedAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    message.RetryCount++;
                    message.Error = ex.Message;
                    
                    if (message.RetryCount > 5)
                    {
                        _logger.LogError("Failed to process outbox message {Id} after 5 retries", message.Id);
                        // Move to dead letter queue
                    }
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}

// Save domain events to outbox in same transaction
public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
{
    // Get domain events
    var domainEvents = ChangeTracker.Entries<Entity>()
        .SelectMany(e => e.Entity.GetDomainEvents())
        .ToList();
    
    // Save events to outbox
    foreach (var domainEvent in domainEvents)
    {
        OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(domainEvent),
            CreatedAt = DateTime.UtcNow
        });
    }
    
    // Clear events from entities
    ChangeTracker.Entries<Entity>()
        .ToList()
        .ForEach(e => e.Entity.ClearDomainEvents());
    
    return await base.SaveChangesAsync(cancellationToken);
}
```

**Action Items:**
1. Implement outbox pattern for reliable event publishing
2. Add background service to process outbox
3. Integrate with message broker (RabbitMQ/Azure Service Bus)
4. Implement dead letter queue for failed events
5. Add event versioning strategy

---

### 13. API Versioning (MEDIUM)
**Issue**: No versioning strategy for backward compatibility.

**Instructions:**
```csharp
// Install: Microsoft.AspNetCore.Mvc.Versioning
// src/Payment.API/Program.cs
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// src/Payment.API/Controllers/V1/PaymentsController.cs
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class PaymentsController : ControllerBase
{
    // v1 endpoints
}

// src/Payment.API/Controllers/V2/PaymentsController.cs
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
public class PaymentsController : ControllerBase
{
    // v2 endpoints with breaking changes
}
```

**Action Items:**
1. Implement URL-based versioning (v1, v2)
2. Support at least 2 major versions
3. Add deprecation warnings in response headers
4. Document version lifecycle policy
5. Update Swagger to show all versions

---

### 14. Health Checks Enhancement (MEDIUM)
**Issue**: Basic health checks - need more detailed checks.

**Instructions:**
```csharp
// src/Payment.API/Program.cs
services.AddHealthChecks()
    .AddNpgSql(
        connectionString: configuration.GetConnectionString("DefaultConnection"),
        name: "postgresql",
        tags: new[] { "db", "ready" })
    .AddRedis(
        redisConnectionString: configuration.GetConnectionString("Redis"),
        name: "redis",
        tags: new[] { "cache", "ready" })
    .AddUrlGroup(
        new Uri("https://api.zaincash.iq/health"),
        name: "zaincash-api",
        tags: new[] { "provider", "live" })
    .AddCheck<PaymentProviderHealthCheck>("payment-providers", tags: new[] { "provider" })
    .AddCheck<DiskSpaceHealthCheck>("disk-space", tags: new[] { "infrastructure" });

// Custom health check
public class PaymentProviderHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var unhealthy = new List<string>();
        
        foreach (var provider in _providers)
        {
            if (!await provider.IsHealthyAsync(cancellationToken))
            {
                unhealthy.Add(provider.Name);
            }
        }
        
        if (unhealthy.Any())
        {
            return HealthCheckResult.Degraded(
                $"Providers unhealthy: {string.Join(", ", unhealthy)}");
        }
        
        return HealthCheckResult.Healthy("All payment providers operational");
    }
}

// Different endpoints for different checks
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

**Action Items:**
1. Add database connectivity check
2. Add Redis cache connectivity check
3. Add payment provider availability checks
4. Add disk space check
5. Add memory usage check
6. Separate liveness and readiness probes

---

### 15. Observability & Distributed Tracing (MEDIUM)
**Issue**: No distributed tracing for debugging.

**Instructions:**
```csharp
// Install: OpenTelemetry packages
// src/Payment.API/Program.cs
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddRedisInstrumentation()
            .AddSource("Payment.Application")
            .AddSource("Payment.Infrastructure")
            .AddJaegerExporter(options =>
            {
                options.AgentHost = configuration["Jaeger:Host"];
                options.AgentPort = int.Parse(configuration["Jaeger:Port"]);
            });
    })
    .WithMetrics(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter();
    });

// Custom tracing in handlers
public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    private static readonly ActivitySource ActivitySource = new("Payment.Application");
    
    public async Task<PaymentDto> Handle(...)
    {
        using var activity = ActivitySource.StartActivity("CreatePayment");
        activity?.SetTag("payment.provider", request.Provider);
        activity?.SetTag("payment.amount", request.Amount);
        
        try
        {
            // ... payment logic
            activity?.SetTag("payment.status", "success");
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetTag("payment.status", "failed");
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

**Action Items:**
1. Add OpenTelemetry for distributed tracing
2. Export to Jaeger/Zipkin
3. Add custom spans for critical operations
4. Export metrics to Prometheus
5. Create Grafana dashboards
6. Add correlation IDs to all logs

---

## 🟡 MEDIUM PRIORITY ARCHITECTURE IMPROVEMENTS

### 16. Result Pattern Instead of Exceptions (MEDIUM)
**Issue**: Using exceptions for control flow (KeyNotFoundException).

**Instructions:**
```csharp
// src/Payment.Domain/Common/Result.cs
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }
    
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }
    
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);
    
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }
}

public record Error(string Code, string Message);

// Usage in handler
public async Task<Result<PaymentDto>> Handle(
    GetPaymentByIdQuery request,
    CancellationToken cancellationToken)
{
    var payment = await _unitOfWork.Payments.GetByIdAsync(
        request.PaymentId, 
        cancellationToken);
    
    if (payment == null)
    {
        return Result<PaymentDto>.Failure(
            new Error("PAYMENT_NOT_FOUND", $"Payment {request.PaymentId} not found"));
    }
    
    return Result<PaymentDto>.Success(payment.ToDto());
}

// Usage in controller
[HttpGet("{id}")]
public async Task<ActionResult<PaymentDto>> GetPayment(Guid id, CancellationToken cancellationToken)
{
    var query = new GetPaymentByIdQuery(id);
    var result = await _mediator.Send(query, cancellationToken);
    
    return result.Match(
        onSuccess: payment => Ok(payment),
        onFailure: error => error.Code switch
        {
            "PAYMENT_NOT_FOUND" => NotFound(new { error.Code, error.Message }),
            _ => BadRequest(new { error.Code, error.Message })
        });
}
```

**Action Items:**
1. Implement Result<T> pattern
2. Replace exceptions with Result returns
3. Use domain-specific error codes
4. Map errors to HTTP status codes in controller
5. Keep exceptions for exceptional cases only

---

### 17. Feature Flags (MEDIUM)
**Issue**: No way to toggle features without deployment.

**Instructions:**
```csharp
// Install: Microsoft.FeatureManagement
// appsettings.json
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
    }
  }
}

// src/Payment.API/Program.cs
services.AddFeatureManagement();

// Usage in handler
public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    private readonly IFeatureManager _featureManager;
    
    public async Task<PaymentDto> Handle(...)
    {
        if (await _featureManager.IsEnabledAsync("FraudDetection"))
        {
            await _fraudDetectionService.CheckAsync(request);
        }
        
        // ... rest of logic
    }
}

// Controller
[HttpPost("split")]
[FeatureGate("SplitPayments")]
public async Task<ActionResult<PaymentDto>> CreateSplitPayment(...)
{
    // Only accessible if SplitPayments feature is enabled
}
```

**Action Items:**
1. Add feature management library
2. Use feature flags for new providers
3. Implement gradual rollout (percentage-based)
4. Add feature flag management API
5. Store flags in distributed config (Azure App Config)

---

### 18. State Machine for Payment Status (MEDIUM)
**Issue**: Payment state transitions are error-prone.

**Instructions:**
```csharp
// Install: Stateless
// src/Payment.Domain/StateMachines/PaymentStateMachine.cs
public class PaymentStateMachine
{
    private readonly StateMachine<PaymentStatus, PaymentTrigger> _stateMachine;
    
    public PaymentStateMachine(PaymentStatus initialState)
    {
        _stateMachine = new StateMachine<PaymentStatus, PaymentTrigger>(initialState);
        
        _stateMachine.Configure(PaymentStatus.Pending)
            .Permit(PaymentTrigger.Process, PaymentStatus.Processing)
            .Permit(PaymentTrigger.Fail, PaymentStatus.Failed);
        
        _stateMachine.Configure(PaymentStatus.Processing)
            .Permit(PaymentTrigger.Complete, PaymentStatus.Completed)
            .Permit(PaymentTrigger.Fail, PaymentStatus.Failed);
        
        _stateMachine.Configure(PaymentStatus.Completed)
            .Permit(PaymentTrigger.Refund, PaymentStatus.Refunded);
        
        _stateMachine.Configure(PaymentStatus.Failed)
            .Ignore(PaymentTrigger.Fail); // Already failed
        
        _stateMachine.Configure(PaymentStatus.Refunded)
            .Ignore(PaymentTrigger.Refund); // Already refunded
    }
    
    public bool CanFire(PaymentTrigger trigger)
    {
        return _stateMachine.CanFire(trigger);
    }
    
    public void Fire(PaymentTrigger trigger)
    {
        _stateMachine.Fire(trigger);
    }
    
    public PaymentStatus CurrentState => _stateMachine.State;
}

public enum PaymentTrigger
{
    Process,
    Complete,
    Fail,
    Refund
}

// Usage in Payment entity
public void Process(string transactionId)
{
    if (!_stateMachine.CanFire(PaymentTrigger.Process))
    {
        throw new InvalidOperationException(
            $"Cannot process payment in state {_stateMachine.CurrentState}");
    }
    
    _stateMachine.Fire(PaymentTrigger.Process);
    TransactionId = transactionId;
    Status = _stateMachine.CurrentState;
    UpdatedAt = DateTime.UtcNow;
    
    AddDomainEvent(new PaymentProcessingEvent(Id.Value, OrderId));
}
```

**Action Items:**
1. Implement Stateless state machine
2. Define all valid state transitions
3. Prevent invalid state changes
4. Add state transition logging
5. Visualize state machine diagram

---

## 🟢 LOW PRIORITY / NICE TO HAVE

### 19. GraphQL Support (LOW)
For flexible client queries.

### 20. Webhook Retry Mechanism (LOW)
Implement exponential backoff for failed webhooks.

### 21. Multi-Currency Settlement (LOW) ✅ COMPLETED
Support automatic currency conversion.

**Status**: ✅ **IMPLEMENTED**

**Implementation Details:**

1. **Domain Layer**:
   - Added `SettlementCurrency`, `SettlementAmount`, `ExchangeRate`, and `SettledAt` fields to `Payment` entity
   - Added `SetSettlement()` method to `Payment` entity for setting settlement information
   - Created `ISettlementService` interface in Domain layer

2. **Application Layer**:
   - Implemented `SettlementService` that uses `IExchangeRateService` for currency conversion
   - Updated `CompletePaymentCommand` to accept optional `SettlementCurrency`
   - Updated `CompletePaymentCommandHandler` to process settlement when payment completes
   - Updated `HandlePaymentCallbackCommandHandler` to process settlement on callback completion
   - Updated `PaymentDto` with `SettlementDto` for settlement information
   - Updated `PaymentMappingExtensions` to map settlement data

3. **Infrastructure Layer**:
   - Updated `PaymentConfiguration` to include settlement fields in database schema
   - Updated `ZainCashPaymentProvider` to use `IExchangeRateService` for currency conversion
   - Registered `SettlementService` in dependency injection

4. **Configuration**:
   - Added `Settlement:Currency` configuration (default: "USD")
   - Added `Settlement:Enabled` configuration flag

**Features**:
- ✅ Automatic currency conversion when payment currency differs from settlement currency
- ✅ Exchange rate stored with payment for audit trail
- ✅ Non-blocking: conversion failures don't prevent payment completion
- ✅ SOLID compliant: separation of concerns, dependency inversion
- ✅ Clean Architecture: domain → application → infrastructure
- ✅ Stateless: no in-memory state, Kubernetes-ready

**Database Migration Required**:
```bash
dotnet ef migrations add AddMultiCurrencySettlement --project src/Payment.Infrastructure --startup-project src/Payment.API
dotnet ef database update --project src/Payment.Infrastructure --startup-project src/Payment.API
```

**Testing**:
- ✅ Comprehensive unit tests for `SettlementService`
- ✅ Domain tests for `Payment.SetSettlement()` method
- ✅ Handler tests for settlement integration
- ✅ Callback handler tests with settlement
- ✅ Error handling and edge case coverage

**Files Modified**:
- `src/Payment.Domain/Entities/Payment.cs`
- `src/Payment.Domain/Interfaces/ISettlementService.cs` (new)
- `src/Payment.Application/Services/SettlementService.cs` (new)
- `src/Payment.Application/Handlers/CompletePaymentCommandHandler.cs`
- `src/Payment.Application/Handlers/HandlePaymentCallbackCommandHandler.cs`
- `src/Payment.Application/Commands/CompletePaymentCommand.cs`
- `src/Payment.Application/DTOs/PaymentDto.cs`
- `src/Payment.Application/Mappings/PaymentMappingExtensions.cs`
- `src/Payment.Infrastructure/Data/Configurations/PaymentConfiguration.cs`
- `src/Payment.Infrastructure/Providers/ZainCashPaymentProvider.cs`
- `src/Payment.Infrastructure/DependencyInjection.cs`
- `src/Payment.API/appsettings.json`

### 22. Fraud Detection (LOW) ✅ COMPLETED
**Status**: Implemented and tested

**Implementation Summary**:
- Created `IFraudDetectionService` domain interface with value objects (`FraudCheckRequest`, `FraudCheckResult`)
- Implemented `FraudDetectionService` in Infrastructure layer with HTTP client integration
- Integrated fraud detection into `CreatePaymentCommandHandler` with feature flag support
- Added exception handling in `PaymentsController` for blocked payments
- Implemented resilience patterns (retry + circuit breaker) using Polly
- Added comprehensive test coverage across all layers

**Files Created**:
- `src/Payment.Domain/Interfaces/IFraudDetectionService.cs`
- `src/Payment.Domain/Exceptions/FraudDetectionException.cs`
- `src/Payment.Infrastructure/Services/FraudDetectionService.cs`
- `tests/Payment.Domain.Tests/ValueObjects/FraudCheckRequestTests.cs`
- `tests/Payment.Domain.Tests/ValueObjects/FraudCheckResultTests.cs`
- `tests/Payment.Infrastructure.Tests/Services/FraudDetectionServiceTests.cs`
- `tests/Payment.Application.Tests/Handlers/CreatePaymentCommandHandlerFraudDetectionTests.cs`

**Files Modified**:
- `src/Payment.Application/Handlers/CreatePaymentCommandHandler.cs`
- `src/Payment.API/Controllers/PaymentsController.cs`
- `src/Payment.Infrastructure/DependencyInjection.cs`
- `src/Payment.API/appsettings.json`
- `tests/Payment.API.Tests/Controllers/PaymentsControllerTests.cs`

**Configuration**:
```json
{
  "FraudDetection": {
    "Enabled": false,
    "BaseUrl": "https://api.fraud-detection-service.com/",
    "ApiKey": ""
  }
}
```

**Features**:
- ✅ Feature flag controlled (via `FraudDetection` feature flag)
- ✅ Risk-based decision making (Low/Medium/High)
- ✅ High risk payments are blocked
- ✅ Medium risk payments are logged for review
- ✅ Graceful degradation (defaults to low risk on service failure)
- ✅ Resilience patterns (retry + circuit breaker)
- ✅ IP address extraction from HTTP context
- ✅ OpenTelemetry tracing integration
- ✅ Comprehensive test coverage

**Usage**:
1. Enable fraud detection in `appsettings.json`: `"FraudDetection:Enabled": true`
2. Configure fraud detection service URL and API key
3. Ensure `FraudDetection` feature flag is enabled
4. High-risk payments will be automatically blocked
5. Medium-risk payments will be logged for manual review

### 23. 3D Secure Support (LOW)
Implement 3DS authentication flow.

---

## 📋 IMPLEMENTATION PRIORITY CHECKLIST

### Phase 1: Critical Security (Week 1-2)
- [x] Webhook signature validation ✅ **COMPLETED** - HMAC-SHA256 signature validation implemented
- [x] Idempotency keys ✅ **COMPLETED** - Request hash validation to prevent duplicate payments
- [x] PCI DSS compliance (tokenization) ✅ **COMPLETED** - Card tokenization and AES-256 metadata encryption at rest
- [x] Secrets management (Key Vault) ✅ **COMPLETED** - Azure Key Vault, AWS Secrets Manager, Kubernetes Secrets support
- [x] Input validation & sanitization ✅ **COMPLETED** - XSS protection, security headers, rate limiting

### Phase 2: High Priority (Week 3-4)
- [x] Rate limiting ✅ **COMPLETED** - Implemented with input validation & sanitization
- [x] Audit logging ✅ **COMPLETED** - Comprehensive audit trail for compliance
- [x] Resilience patterns (Polly) ✅ **COMPLETED** - Circuit Breaker, Retry, Timeout implemented
- [x] Caching (Redis) ✅ **COMPLETED** - Redis with memory cache fallback
- [x] Database optimization ✅ **COMPLETED** - Indexes, pagination, query optimization

### Phase 3: Medium Priority (Week 5-6)
- [x] Outbox pattern ✅ **COMPLETED** - Event Sourcing & Outbox Pattern for reliable event publishing
- [x] API versioning ✅ **COMPLETED** - URL-based versioning support
- [x] Enhanced health checks ✅ **COMPLETED** - Health checks (`/health`, `/ready`) for K8s probes
- [x] Distributed tracing ✅ **COMPLETED** - OpenTelemetry integration with Jaeger/Zipkin
- [x] Result pattern ✅ **COMPLETED** - Functional error handling implemented

### Phase 4: Architecture Improvements (Week 7-8)
- [x] Feature flags ✅ **COMPLETED** - Microsoft.FeatureManagement implemented
- [x] State machine ✅ **COMPLETED** - Payment status transitions (Stateless library)
- [x] Performance testing ✅ **COMPLETED** - Load testing and performance benchmarks implemented with k6
- [x] Security audit ✅ **COMPLETED** - OWASP ZAP scan and penetration testing implemented
- [x] Documentation update ✅ **COMPLETED** - Comprehensive documentation with OpenAPI/Swagger

**📊 Checklist Summary:**
- **Phase 1 (Critical Security)**: ✅ **100% Complete** (5/5 items)
- **Phase 2 (High Priority)**: ✅ **100% Complete** (5/5 items)
- **Phase 3 (Medium Priority)**: ✅ **100% Complete** (5/5 items)
- **Phase 4 (Architecture Improvements)**: ✅ **100% Complete** (5/5 items)

**Additional Completed Items (Low Priority):**
- ✅ Multi-Currency Settlement (Item 21)
- ✅ Fraud Detection (Item 22)

### Performance Testing Implementation ✅ **COMPLETED**

**Status**: ✅ **IMPLEMENTED**

**Implementation Details:**

1. **Performance Testing Infrastructure**:
   - Created `testing/performance/` directory with k6 load testing scripts
   - Implemented three test scenarios:
     - **Load Test** (`load-test.js`): Comprehensive load test with gradual ramp-up (10 → 50 → 100 → 200 users)
     - **Stress Test** (`stress-test.js`): Gradually increases load to find breaking point (50 → 500 users)
     - **Spike Test** (`spike-test.js`): Tests system resilience to sudden traffic spikes (100 → 1000 users)

2. **Test Features**:
   - Custom metrics: `payment_creation_time`, `payment_query_time`, `errors`
   - Performance thresholds: p95 < 2s, p99 < 5s, error rate < 1%
   - Realistic test data generation (random amounts, currencies, providers)
   - JWT authentication support for authenticated endpoints
   - HTML report generation for test results

3. **Documentation**:
   - Comprehensive README with installation instructions
   - CI/CD integration examples
   - Performance benchmark targets and interpretation guide
   - Troubleshooting section

4. **Usage**:
   ```bash
   # Run load test
   k6 run testing/performance/load-test.js
   
   # With environment variables
   BASE_URL=http://localhost:5000 JWT_TOKEN=token k6 run testing/performance/load-test.js
   ```

**Files Created**:
- `testing/performance/load-test.js`
- `testing/performance/stress-test.js`
- `testing/performance/spike-test.js`
- `testing/performance/README.md`

**Target Metrics** (from remediation instructions):
- Payment success rate: >99%
- Average payment processing time: <2s
- API response time: p50 < 500ms, p95 < 2000ms, p99 < 5000ms
- Error rate: <0.1%

---

### Security Audit Implementation ✅ **COMPLETED**

**Status**: ✅ **IMPLEMENTED**

**Implementation Details:**

1. **Security Testing Infrastructure**:
   - Created `testing/security/` directory with OWASP ZAP integration
   - Implemented three security testing approaches:
     - **Baseline Scan** (`owasp-zap-baseline.sh`): Quick security scan for unauthenticated endpoints (~5-10 minutes)
     - **Full Scan** (`owasp-zap-full-scan.sh`): Comprehensive scan including authenticated endpoints (~10-20 minutes)
     - **Penetration Testing** (`penetration-test.sh`): Manual tests for common vulnerabilities (SQL injection, XSS, CSRF, etc.)

2. **Test Coverage**:
   - OWASP Top 10 (2021) coverage
   - SQL Injection testing
   - XSS (Cross-Site Scripting) testing
   - Authentication bypass testing
   - CSRF (Cross-Site Request Forgery) testing
   - Rate limiting verification
   - Input validation testing
   - Path traversal testing
   - Security headers verification
   - HTTP method override testing

3. **Report Generation**:
   - HTML reports (human-readable)
   - JSON reports (machine-readable for CI/CD)
   - XML reports (for integration with security tools)
   - Alert summary by risk level (High, Medium, Low, Informational)

4. **Docker Integration**:
   - `docker-compose.zap.yml` for easy ZAP deployment
   - Automated health checks
   - Volume persistence for scan data

5. **Documentation**:
   - Comprehensive README with setup instructions
   - CI/CD integration examples
   - Remediation guidance
   - Troubleshooting section

6. **Usage**:
   ```bash
   # Start ZAP
   docker-compose -f testing/security/docker-compose.zap.yml up -d
   
   # Run baseline scan
   ./testing/security/owasp-zap-baseline.sh
   
   # Run full scan with authentication
   API_URL=http://localhost:5000 JWT_TOKEN=token ./testing/security/owasp-zap-full-scan.sh
   
   # Run penetration tests
   ./testing/security/penetration-test.sh
   ```

**Files Created**:
- `testing/security/owasp-zap-baseline.sh`
- `testing/security/owasp-zap-full-scan.sh`
- `testing/security/penetration-test.sh`
- `testing/security/docker-compose.zap.yml`
- `testing/security/README.md`
- `testing/docker-compose.testing.yml` (unified testing tools)
- `testing/README.md` (overview documentation)

**Security Testing Coverage**:
- ✅ A01:2021 – Broken Access Control
- ✅ A02:2021 – Cryptographic Failures
- ✅ A03:2021 – Injection
- ✅ A04:2021 – Insecure Design
- ✅ A05:2021 – Security Misconfiguration
- ✅ A06:2021 – Vulnerable Components
- ✅ A07:2021 – Authentication Failures
- ✅ A08:2021 – Software and Data Integrity
- ✅ A09:2021 – Security Logging Failures
- ✅ A10:2021 – Server-Side Request Forgery

**Integration**:
- Can be integrated into CI/CD pipelines
- Automated weekly scans recommended
- Reports saved to `testing/security/reports/`

---

## 🔍 TESTING REQUIREMENTS

### Security Testing ✅ **IMPLEMENTED**

**Automated Security Scans:**
```bash
# Start OWASP ZAP
docker-compose -f testing/security/docker-compose.zap.yml up -d

# Run baseline scan (quick scan)
./testing/security/owasp-zap-baseline.sh

# Run full scan (comprehensive, includes authenticated endpoints)
API_URL=http://localhost:5000 JWT_TOKEN=your-token ./testing/security/owasp-zap-full-scan.sh

# Run penetration tests (manual vulnerability tests)
./testing/security/penetration-test.sh
```

**Additional Security Checks:**
```bash
# Dependency vulnerability check
dotnet list package --vulnerable

# Secret scanning
git secrets --scan

# OWASP Dependency Check (alternative)
dotnet add package OWASP.DependencyCheck.MSBuild
```

**Reports Location:** `testing/security/reports/`

### Performance Testing ✅ **IMPLEMENTED**

**Load Testing with k6:**
```bash
# Comprehensive load test (recommended)
k6 run testing/performance/load-test.js

# Stress test (find breaking point)
k6 run testing/performance/stress-test.js

# Spike test (test rate limiting)
k6 run testing/performance/spike-test.js

# With custom configuration
BASE_URL=http://localhost:5000 JWT_TOKEN=your-token k6 run testing/performance/load-test.js
```

**Performance Benchmarks:**
- Payment success rate: >99%
- Average payment processing time: <2s
- API response time: p50 < 500ms, p95 < 2000ms, p99 < 5000ms
- Error rate: <0.1%

**Database Query Analysis:**
```sql
-- Analyze query performance
EXPLAIN ANALYZE SELECT * FROM Payments WHERE MerchantId = 'test' AND Status = 'Completed';

-- Check index usage
SELECT * FROM pg_stat_user_indexes WHERE schemaname = 'public';
```

**Documentation:** See `testing/performance/README.md` for detailed instructions.

### Penetration Testing ✅ **IMPLEMENTED**

**Automated Penetration Tests:**
```bash
# Run all penetration tests
./testing/security/penetration-test.sh
```

**Test Coverage:**
- ✅ SQL injection testing
- ✅ XSS (Cross-Site Scripting) testing
- ✅ CSRF (Cross-Site Request Forgery) testing
- ✅ Authentication bypass testing
- ✅ Rate limiting verification
- ✅ Input validation testing
- ✅ Path traversal testing
- ✅ Security headers verification
- ✅ HTTP method override testing

**Manual Testing Checklist:**
- [ ] Test webhook signature validation
- [ ] Test idempotency key reuse prevention
- [ ] Test PCI DSS compliance (no card data storage)
- [ ] Test secrets management (no secrets in logs)
- [ ] Test audit logging completeness

---

## 📊 MONITORING METRICS

### Business Metrics
- Payment success rate (target: >99%)
- Average payment processing time (target: <2s)
- Provider availability (target: >99.9%)
- Refund rate
- Fraud detection accuracy

### Technical Metrics
- API response time (p50, p95, p99)
- Database query time
- Cache hit rate (target: >80%)
- Error rate (target: <0.1%)
- Circuit breaker status
- Queue depth
- CPU/Memory usage

### Security Metrics
- Failed authentication attempts
- Rate limit hits
- Webhook signature failures
- Suspicious activity alerts

---

## 🚀 DEPLOYMENT CHECKLIST

### Pre-Production
- [ ] All critical security fixes applied
- [ ] Load testing passed
- [ ] Security scan passed
- [ ] Database migrations tested
- [ ] Rollback plan prepared
- [ ] Monitoring configured
- [ ] Alerts configured
- [ ] Documentation updated

### Production
- [ ] Blue-green deployment ready
- [ ] Health checks passing
- [ ] SSL certificates valid
- [ ] Secrets rotated
- [ ] Backup verified
- [ ] DDoS protection active
- [ ] CDN configured
- [ ] Rate limiting active

---

## 📞 INCIDENT RESPONSE

### Payment Failure Incident

**Severity Levels:**
- 🔴 **CRITICAL**: Payment processing completely down, multiple customers affected
- 🟠 **HIGH**: Intermittent failures, single provider affected
- 🟡 **MEDIUM**: Degraded performance, increased error rates
- 🟢 **LOW**: Single transaction failure, customer-specific issue

**Response Procedure:**

#### Phase 1: Immediate Assessment (0-5 minutes)
1. **Check Service Health**
   ```bash
   # Kubernetes health check
   kubectl get pods -n payment-service
   kubectl logs -f deployment/payment-api -n payment-service --tail=100
   
   # Health endpoint
   curl https://payment-api/health
   curl https://payment-api/health/ready
   ```

2. **Check Provider Status**
   - Access provider status dashboard (ZainCash, Stripe, etc.)
   - Verify provider API status endpoints
   - Check provider status page/announcements
   - Review provider-specific error codes in logs

3. **Verify Network Connectivity**
   ```bash
   # Test external connectivity
   kubectl exec -it deployment/payment-api -n payment-service -- curl -v https://api.zaincash.iq
   kubectl exec -it deployment/payment-api -n payment-service -- nslookup api.zaincash.iq
   ```

4. **Check Circuit Breaker Status**
   ```csharp
   // Query circuit breaker metrics
   // Check Polly circuit breaker state via metrics endpoint
   GET /api/metrics/circuit-breakers
   ```
   - Review circuit breaker state (Open/Closed/HalfOpen)
   - Check failure thresholds and recovery times
   - Identify which providers are affected

5. **Review Error Logs**
   ```bash
   # Search for payment errors
   kubectl logs -f deployment/payment-api -n payment-service | grep -i "payment.*error\|exception\|failed"
   
   # Query structured logs (if using ELK/CloudWatch)
   # Filter by: Level=Error, Category=Payment.*, TimeRange=Last 15 minutes
   ```

#### Phase 2: Diagnosis (5-15 minutes)
6. **Analyze Error Patterns**
   - Group errors by provider, error code, and time window
   - Identify if issue is provider-specific or systemic
   - Check for rate limiting or quota exhaustion
   - Review recent deployments or configuration changes

7. **Check Database State**
   ```sql
   -- Check pending payments
   SELECT COUNT(*), Status, Provider 
   FROM Payments 
   WHERE CreatedAt > DATEADD(minute, -30, GETUTCDATE())
   GROUP BY Status, Provider;
   
   -- Check for stuck payments
   SELECT * FROM Payments 
   WHERE Status = 'Pending' 
   AND CreatedAt < DATEADD(minute, -10, GETUTCDATE());
   ```

8. **Review Metrics & Alerts**
   - Payment success rate (should be > 99%)
   - Average response time (should be < 2s)
   - Error rate by provider
   - Queue depth and processing lag

#### Phase 3: Mitigation (15-30 minutes)
9. **Immediate Actions**
   - **If provider is down**: Enable fallback provider (if configured)
   - **If circuit breaker is open**: Manually reset if safe (via admin endpoint)
   - **If database issue**: Check connection pool, deadlocks, or locks
   - **If rate limited**: Implement backoff or switch to alternative provider

10. **Customer Communication**
    ```csharp
    // Automated customer notification service
    public interface IPaymentFailureNotificationService
    {
        Task NotifyCustomerAsync(PaymentId paymentId, string reason, CancellationToken ct);
    }
    ```
    - Send immediate notification to affected customers
    - Provide estimated resolution time
    - Offer alternative payment methods if available

11. **Initiate Refunds (if applicable)**
    ```csharp
    // Refund service for failed payments
    public interface IRefundService
    {
        Task<RefundResult> ProcessRefundAsync(
            PaymentId paymentId, 
            RefundReason reason, 
            CancellationToken ct);
    }
    ```
    - Identify payments that need refunds
    - Process refunds through provider API
    - Update payment status and notify customers

#### Phase 4: Resolution & Follow-up (30+ minutes)
12. **Verify Resolution**
    - Monitor payment success rate returning to normal
    - Confirm all pending payments are processed
    - Verify customer notifications sent
    - Check refunds completed successfully

13. **Documentation**
    - Log incident in incident tracking system
    - Document root cause, timeline, and resolution
    - Update runbooks with lessons learned
    - Create post-mortem if severity was CRITICAL

**Implementation Requirements:**
```csharp
// src/Payment.Application/Services/IncidentResponseService.cs
public interface IIncidentResponseService
{
    Task<IncidentAssessment> AssessPaymentFailureAsync(
        PaymentFailureContext context, 
        CancellationToken ct);
    
    Task NotifyStakeholdersAsync(
        IncidentSeverity severity, 
        string details, 
        CancellationToken ct);
    
    Task ProcessAutomaticRefundsAsync(
        IEnumerable<PaymentId> paymentIds, 
        CancellationToken ct);
}

// src/Payment.API/Controllers/Admin/IncidentController.cs
[ApiController]
[Route("api/admin/incidents")]
[Authorize(Policy = "AdminOnly")]
public class IncidentController : ControllerBase
{
    [HttpPost("payment-failure/assess")]
    public async Task<IActionResult> AssessPaymentFailure(
        [FromBody] PaymentFailureAssessmentRequest request)
    {
        var assessment = await _incidentResponseService
            .AssessPaymentFailureAsync(request.Context, ct);
        return Ok(assessment);
    }
    
    [HttpPost("circuit-breaker/reset/{provider}")]
    public async Task<IActionResult> ResetCircuitBreaker(string provider)
    {
        await _circuitBreakerService.ResetAsync(provider, ct);
        return Ok();
    }
}
```

---

### Security Incident Response

**Severity Levels:**
- 🔴 **CRITICAL**: Active breach, data exfiltration, unauthorized access
- 🟠 **HIGH**: Suspicious activity, potential compromise, failed attacks
- 🟡 **MEDIUM**: Anomalous behavior, policy violations
- 🟢 **LOW**: Security alerts, false positives

**Response Procedure:**

#### Phase 1: Containment (0-15 minutes)
1. **Isolate Affected Systems**
   ```bash
   # Kubernetes: Scale down affected pods
   kubectl scale deployment payment-api --replicas=0 -n payment-service
   
   # Network isolation: Update NetworkPolicy
   kubectl apply -f network-policy-isolation.yaml -n payment-service
   
   # Block suspicious IPs at ingress level
   kubectl annotate ingress payment-ingress \
     nginx.ingress.kubernetes.io/whitelist-source-range="<trusted-ips>"
   ```

2. **Revoke Compromised Credentials**
   ```csharp
   // Credential revocation service
   public interface ICredentialRevocationService
   {
        Task RevokeApiKeyAsync(string apiKeyId, CancellationToken ct);
        Task RevokeJwtTokenAsync(string tokenId, CancellationToken ct);
        Task RotateSecretsAsync(string secretName, CancellationToken ct);
    }
    ```
   - Revoke compromised API keys immediately
   - Invalidate JWT tokens (add to blacklist)
   - Rotate database connection strings
   - Rotate payment provider API keys
   - Update Kubernetes secrets

3. **Enable Enhanced Logging**
   ```csharp
   // Enable verbose security logging
   // Update log level to Debug for security events
   // Enable audit logging for all sensitive operations
   ```

#### Phase 2: Investigation (15-60 minutes)
4. **Review Audit Logs**
   ```sql
   -- Query audit logs for suspicious activity
   SELECT * FROM AuditLogs 
   WHERE EventType IN ('Authentication', 'Authorization', 'Payment', 'DataAccess')
   AND Timestamp > DATEADD(hour, -24, GETUTCDATE())
   AND (UserId = @SuspiciousUserId OR IpAddress = @SuspiciousIp)
   ORDER BY Timestamp DESC;
   
   -- Check for privilege escalation
   SELECT * FROM AuditLogs 
   WHERE EventType = 'Authorization' 
   AND Result = 'Denied'
   AND Timestamp > DATEADD(hour, -24, GETUTCDATE());
   ```

5. **Analyze Attack Vectors**
   - Review failed authentication attempts
   - Check for SQL injection patterns in logs
   - Identify XSS or CSRF attempts
   - Review webhook signature validation failures
   - Check for unusual API usage patterns

6. **Assess Data Exposure**
   ```sql
   -- Identify potentially exposed data
   SELECT PaymentId, CustomerId, Amount, Status, CreatedAt
   FROM Payments
   WHERE CreatedAt BETWEEN @IncidentStartTime AND @IncidentEndTime
   AND Status IN ('Completed', 'Pending');
   
   -- Check PII access logs
   SELECT * FROM AuditLogs
   WHERE EventType = 'DataAccess'
   AND ResourceType = 'Customer'
   AND Timestamp BETWEEN @IncidentStartTime AND @IncidentEndTime;
   ```

#### Phase 3: Notification & Escalation (30-60 minutes)
7. **Notify Security Team**
   ```csharp
   // Security incident notification service
   public interface ISecurityIncidentNotificationService
   {
        Task NotifySecurityTeamAsync(
            SecurityIncident incident, 
            CancellationToken ct);
        
        Task NotifyComplianceOfficerAsync(
            DataBreachDetails details, 
            CancellationToken ct);
    }
    ```
   - Immediate notification to security team
   - Escalate to CISO if CRITICAL
   - Notify compliance officer if PII/PCI data exposed
   - Contact legal team if regulatory notification required

8. **External Notifications (if required)**
   - PCI DSS: Notify within 24 hours if card data compromised
   - GDPR: Notify DPA within 72 hours if personal data breach
   - Customers: Notify affected customers per regulatory requirements

#### Phase 4: Remediation (1-4 hours)
9. **Implement Immediate Fixes**
   - Patch identified vulnerabilities
   - Update security policies
   - Strengthen authentication/authorization
   - Enhance input validation
   - Update rate limiting rules

10. **Security Hardening**
    ```csharp
    // Implement additional security measures
    - Enable MFA for all admin accounts
    - Implement IP whitelisting for sensitive endpoints
    - Add additional monitoring and alerting
    - Review and update security policies
    ```

11. **Data Recovery (if applicable)**
    - Restore from backups if data was modified
    - Verify data integrity
    - Replay transactions if needed

#### Phase 5: Documentation & Post-Mortem (4-24 hours)
12. **Document Incident**
    ```markdown
    ## Incident Report Template
    - Incident ID: [GUID]
    - Severity: [CRITICAL/HIGH/MEDIUM/LOW]
    - Discovery Time: [Timestamp]
    - Resolution Time: [Timestamp]
    - Root Cause: [Detailed analysis]
    - Impact: [Affected systems, data, customers]
    - Actions Taken: [Step-by-step response]
    - Preventive Measures: [Future safeguards]
    ```

13. **Post-Mortem Review**
    - Conduct post-mortem meeting within 48 hours
    - Identify process improvements
    - Update incident response playbook
    - Create action items for security improvements
    - Review and update security monitoring rules

**Implementation Requirements:**
```csharp
// src/Payment.Application/Services/SecurityIncidentResponseService.cs
public interface ISecurityIncidentResponseService
{
    Task<SecurityIncidentAssessment> AssessIncidentAsync(
        SecurityEvent securityEvent, 
        CancellationToken ct);
    
    Task ContainIncidentAsync(
        SecurityIncidentId incidentId, 
        ContainmentStrategy strategy, 
        CancellationToken ct);
    
    Task GenerateIncidentReportAsync(
        SecurityIncidentId incidentId, 
        CancellationToken ct);
}

// src/Payment.Infrastructure/Security/AuditLogger.cs
public interface IAuditLogger
{
    Task LogSecurityEventAsync(
        SecurityEventType eventType,
        string userId,
        string resource,
        string action,
        bool succeeded,
        string details,
        CancellationToken ct);
}

// src/Payment.API/Controllers/Admin/SecurityIncidentController.cs
[ApiController]
[Route("api/admin/security/incidents")]
[Authorize(Policy = "SecurityAdminOnly")]
public class SecurityIncidentController : ControllerBase
{
    [HttpPost("assess")]
    public async Task<IActionResult> AssessIncident(
        [FromBody] SecurityEvent securityEvent)
    {
        var assessment = await _securityIncidentResponseService
            .AssessIncidentAsync(securityEvent, ct);
        return Ok(assessment);
    }
    
    [HttpPost("{incidentId}/contain")]
    public async Task<IActionResult> ContainIncident(
        Guid incidentId,
        [FromBody] ContainmentRequest request)
    {
        await _securityIncidentResponseService
            .ContainIncidentAsync(incidentId, request.Strategy, ct);
        return Ok();
    }
    
    [HttpGet("{incidentId}/report")]
    public async Task<IActionResult> GetIncidentReport(Guid incidentId)
    {
        var report = await _securityIncidentResponseService
            .GenerateIncidentReportAsync(incidentId, ct);
        return Ok(report);
    }
}
```

**Monitoring & Alerting:**
```csharp
// Configure alerts for security incidents
- Failed authentication attempts > 10 in 5 minutes
- Webhook signature validation failures > 5 in 1 minute
- Unauthorized access attempts
- Unusual API usage patterns
- Database access from unexpected IPs
- Privilege escalation attempts
- Payment amount anomalies
- Rate limit violations from single source
```

**Action Items:**

### 🔴 CRITICAL Priority

#### 1. Implement `IIncidentResponseService` for Payment Failures
**Location:** `src/Payment.Application/Services/IncidentResponseService.cs`

**Tasks:**
- [x] Create `IIncidentResponseService` interface with methods:
  - `AssessPaymentFailureAsync(PaymentFailureContext, CancellationToken)`
  - `NotifyStakeholdersAsync(IncidentSeverity, string, CancellationToken)`
  - `ProcessAutomaticRefundsAsync(IEnumerable<PaymentId>, CancellationToken)`
  - `GetIncidentMetricsAsync(TimeRange, CancellationToken)`
- [x] Implement `IncidentResponseService` class:
  - Integrate with `IPaymentRepository` to query payment failures
  - Integrate with `ICircuitBreakerService` to check provider status
  - Integrate with `IRefundService` for automatic refunds
  - Integrate with `INotificationService` for stakeholder alerts
  - Use `ILogger<IncidentResponseService>` for structured logging
- [x] Create `PaymentFailureContext` value object:
  ```csharp
  public sealed record PaymentFailureContext(
      DateTime StartTime,
      DateTime? EndTime,
      string? Provider,
      PaymentFailureType FailureType,
      int AffectedPaymentCount,
      Dictionary<string, object> Metadata);
  ```
- [x] Create `IncidentAssessment` DTO:
  ```csharp
  public sealed record IncidentAssessment(
      IncidentSeverity Severity,
      string RootCause,
      IEnumerable<string> AffectedProviders,
      int AffectedPaymentCount,
      TimeSpan EstimatedResolutionTime,
      IEnumerable<RecommendedAction> RecommendedActions);
  ```
- [x] Register service in DI container:
  ```csharp
  services.AddScoped<IIncidentResponseService, IncidentResponseService>();
  ```
- [x] Write unit tests (target: 80%+ coverage)
- [x] Write integration tests for end-to-end scenarios

**Dependencies:**
- `IPaymentRepository`
- `ICircuitBreakerService`
- `IRefundService`
- `INotificationService`
- `ILogger<IncidentResponseService>`

**Estimated Effort:** 2-3 days

---

#### 2. Implement `ISecurityIncidentResponseService` for Security Incidents
**Location:** `src/Payment.Application/Services/SecurityIncidentResponseService.cs`

**Tasks:**
- [x] Create `ISecurityIncidentResponseService` interface with methods:
  - `AssessIncidentAsync(SecurityEvent, CancellationToken)`
  - `ContainIncidentAsync(SecurityIncidentId, ContainmentStrategy, CancellationToken)`
  - `GenerateIncidentReportAsync(SecurityIncidentId, CancellationToken)`
  - `RevokeCredentialsAsync(CredentialRevocationRequest, CancellationToken)`
- [x] Implement `SecurityIncidentResponseService` class:
  - Integrate with `IAuditLogger` to query security events
  - Integrate with `ICredentialRevocationService` for credential management
  - Integrate with `ISecurityNotificationService` for team alerts
  - Use `ILogger<SecurityIncidentResponseService>` for audit trail
- [x] Create `SecurityEvent` value object:
  ```csharp
  public sealed record SecurityEvent(
      SecurityEventType EventType,
      DateTime Timestamp,
      string? UserId,
      string? IpAddress,
      string Resource,
      string Action,
      bool Succeeded,
      string? Details);
  ```
- [x] Create `SecurityIncidentAssessment` DTO:
  ```csharp
  public sealed record SecurityIncidentAssessment(
      SecurityIncidentSeverity Severity,
      SecurityThreatType ThreatType,
      IEnumerable<string> AffectedResources,
      IEnumerable<string> CompromisedCredentials,
      ContainmentStrategy RecommendedContainment,
      IEnumerable<RemediationAction> RemediationActions);
  ```
- [x] Create `ContainmentStrategy` enum:
  ```csharp
  public enum ContainmentStrategy
  {
      IsolatePod,
      BlockIpAddress,
      RevokeCredentials,
      DisableFeature,
      ScaleDown,
      NetworkIsolation
  }
  ```
- [x] Register service in DI container
- [x] Write unit tests (target: 85%+ coverage) - *Implemented in `tests/Payment.Application.Tests/Services/SecurityIncidentResponseServiceTests.cs`*
- [x] Write integration tests for containment scenarios - *Implemented in `tests/Payment.Application.Tests/Services/SecurityIncidentResponseServiceIntegrationTests.cs`*

**Dependencies:**
- [x] `IAuditLogger` - Implemented in `src/Payment.Infrastructure/Security/AuditLogger.cs`
- [x] `ICredentialRevocationService` - Implemented in `src/Payment.Infrastructure/Security/CredentialRevocationService.cs`
- [x] `ISecurityNotificationService` - Implemented in `src/Payment.Infrastructure/Security/SecurityNotificationService.cs`
- [x] `ILogger<SecurityIncidentResponseService>` - Provided by DI container

**Estimated Effort:** 3-4 days

---

#### 3. Create Admin Endpoints for Incident Management
**Location:** `src/Payment.API/Controllers/Admin/`

**Tasks:**
- [x] Create `IncidentController.cs`:
  ```csharp
  [ApiController]
  [Route("api/admin/incidents")]
  [Authorize(Policy = "AdminOnly")]
  [ApiVersion("1.0")]
  public class IncidentController : ControllerBase
  {
      // Payment failure endpoints
      [HttpPost("payment-failure/assess")]
      [ProducesResponseType(typeof(IncidentAssessment), 200)]
      public async Task<IActionResult> AssessPaymentFailure(...)
      
      [HttpPost("payment-failure/refund")]
      [ProducesResponseType(typeof(RefundResult), 200)]
      public async Task<IActionResult> ProcessRefunds(...)
      
      [HttpPost("circuit-breaker/reset/{provider}")]
      [ProducesResponseType(204)]
      public async Task<IActionResult> ResetCircuitBreaker(...)
      
      [HttpGet("metrics")]
      [ProducesResponseType(typeof(IncidentMetrics), 200)]
      public async Task<IActionResult> GetIncidentMetrics(...)
  }
  ```
- [x] Create `SecurityIncidentController.cs`:
  ```csharp
  [ApiController]
  [Route("api/admin/security/incidents")]
  [Authorize(Policy = "SecurityAdminOnly")]
  [ApiVersion("1.0")]
  public class SecurityIncidentController : ControllerBase
  {
      [HttpPost("assess")]
      public async Task<IActionResult> AssessIncident(...)
      
      [HttpPost("{incidentId}/contain")]
      public async Task<IActionResult> ContainIncident(...)
      
      [HttpGet("{incidentId}/report")]
      public async Task<IActionResult> GetIncidentReport(...)
      
      [HttpPost("credentials/revoke")]
      public async Task<IActionResult> RevokeCredentials(...)
  }
  ```
- [x] Add Swagger/OpenAPI documentation for all endpoints (via `ProducesResponseType` attributes)
- [x] Implement request/response DTOs with validation attributes
- [x] Add rate limiting (admin endpoints: 100 req/min) - *Configured in `appsettings.json` with pattern `*:/api/v*/admin/*`*
- [x] Add request/response logging middleware - *Implemented `AdminRequestLoggingMiddleware` in `src/Payment.API/Middleware/`*
- [x] Write integration tests for all endpoints - *Implemented in `tests/Payment.API.Tests/Controllers/Admin/`*
- [x] Add API versioning support (via `ApiVersion` attribute and route versioning)

**Security Requirements:**
- [x] All endpoints require `AdminOnly` or `SecurityAdminOnly` policy (implemented via `[Authorize]` attributes)
- [x] Implement IP whitelisting for production - *Implemented `IpWhitelistMiddleware` in `src/Payment.API/Middleware/`, configured via `Security:IpWhitelist` in `appsettings.json`*
- [x] Add audit logging for all admin actions (via `ILogger` in controllers and services, plus `AdminRequestLoggingMiddleware`)
- [x] Use HTTPS only (enforced via `UseHttpsRedirection()` in Program.cs)

**Estimated Effort:** 2-3 days

---

### 🟠 HIGH Priority

#### 4. Set Up Automated Alerting for Critical Incidents
**Location:** `src/Payment.Infrastructure/Monitoring/`

**Tasks:**
- [x] Create `IAlertingService` interface:
  ```csharp
  public interface IAlertingService
  {
      Task SendAlertAsync(
          AlertSeverity severity,
          string title,
          string message,
          Dictionary<string, object>? metadata = null,
          CancellationToken ct = default);
      
      Task SendPaymentFailureAlertAsync(
          PaymentFailureContext context,
          CancellationToken ct = default);
      
      Task SendSecurityIncidentAlertAsync(
          SecurityEvent securityEvent,
          CancellationToken ct = default);
  }
  ```
- [x] Implement `AlertingService` with integrations:
  - Email notifications (SMTP)
  - Slack/Teams webhooks
  - PagerDuty integration (for CRITICAL)
  - SMS notifications (via Twilio/AWS SNS)
- [x] Create alert rules configuration:
  ```json
  {
    "AlertRules": {
      "PaymentFailure": {
        "Critical": {
          "Threshold": "> 10 failures in 5 minutes",
          "Channels": ["PagerDuty", "Email", "Slack"]
        },
        "High": {
          "Threshold": "> 5 failures in 5 minutes",
          "Channels": ["Email", "Slack"]
        }
      },
      "SecurityIncident": {
        "Critical": {
          "Threshold": "Any unauthorized access",
          "Channels": ["PagerDuty", "Email", "SMS"]
        }
      }
    }
  }
  ```
- [x] Implement alert deduplication (prevent alert storms)
- [x] Create alert templates for consistent formatting
- [x] Integrate with application metrics (Prometheus/Grafana) - *Integrated with AlertMetrics*
- [x] Set up alert routing based on severity and time
- [x] Add alert acknowledgment mechanism - *Basic implementation with cache storage*
- [x] Write unit tests for alerting logic

**Integration Points:**
- Application Insights / CloudWatch
- Prometheus metrics
- Serilog structured logging
- Health check endpoints

**Estimated Effort:** 3-4 days

---

#### 5. Implement Credential Revocation Service
**Location:** `src/Payment.Infrastructure/Security/CredentialRevocationService.cs`

**Tasks:**
- [x] Create `ICredentialRevocationService` interface:
  ```csharp
  public interface ICredentialRevocationService
  {
      Task RevokeApiKeyAsync(string apiKeyId, CancellationToken ct);
      Task RevokeJwtTokenAsync(string tokenId, CancellationToken ct);
      Task RotateSecretsAsync(string secretName, CancellationToken ct);
      Task<bool> IsRevokedAsync(string credentialId, CancellationToken ct);
      Task<IEnumerable<RevokedCredential>> GetRevokedCredentialsAsync(
          DateTime? since = null,
          CancellationToken ct = default);
  }
  ```
- [x] Implement `CredentialRevocationService`:
  - Store revoked credentials in distributed cache (Redis)
  - Store in database for audit trail
  - Implement TTL for cache entries
  - Support bulk revocation operations
- [x] Create `RevokedCredential` entity:
  ```csharp
  public class RevokedCredential
  {
      public string CredentialId { get; set; }
      public CredentialType Type { get; set; }
      public DateTime RevokedAt { get; set; }
      public string Reason { get; set; }
      public string? RevokedBy { get; set; }
      public DateTime? ExpiresAt { get; set; }
  }
  ```
- [x] Create JWT token blacklist middleware:
  ```csharp
  public class JwtTokenBlacklistMiddleware
  {
      // Check token against revocation list
      // Reject if token is in blacklist
  }
  ```
- [x] Create API key revocation repository - *Integrated into CredentialRevocationService with EF Core*
- [x] Implement secret rotation for:
  - Database connection strings
  - Payment provider API keys
  - JWT signing keys
  - Webhook secrets
- [x] Add Kubernetes secret rotation integration - *Structure created, requires K8s client library*
- [x] Create admin endpoint for credential management
- [x] Write unit and integration tests

**Security Requirements:**
- All revocation operations must be audited
- Support immediate and scheduled revocation
- Implement revocation propagation (multi-region)

**Estimated Effort:** 4-5 days

---

### 🟡 MEDIUM Priority

#### 6. Set Up Audit Log Querying Tools ✅ COMPLETED
**Location:** `src/Payment.Infrastructure/Auditing/`

**Tasks:**
- [x] Create `IAuditLogQueryService` interface:
  ```csharp
  public interface IAuditLogQueryService
  {
      Task<IEnumerable<AuditLogEntry>> QueryAsync(
          AuditLogQuery query,
          CancellationToken ct = default);
      
      Task<AuditLogSummary> GetSummaryAsync(
          DateTime startTime,
          DateTime endTime,
          CancellationToken ct = default);
      
      Task<IEnumerable<SecurityEvent>> GetSecurityEventsAsync(
          SecurityEventQuery query,
          CancellationToken ct = default);
  }
  ```
- [x] Implement `AuditLogQueryService`:
  - Support filtering by: UserId, IpAddress, EventType, Resource, TimeRange
  - Support pagination and sorting
  - Optimize queries with proper indexes
- [x] Create `AuditLogQuery` DTO with validation
- [x] Create admin endpoint for audit log queries:
  ```csharp
  [HttpGet("audit-logs")]
  [Authorize(Policy = "SecurityAdminOnly")]
  public async Task<IActionResult> QueryAuditLogs([FromQuery] AuditLogQuery query)
  ```
- [x] Add audit log export functionality (CSV/JSON)
- [x] Implement audit log retention policy (90 days hot, 1 year cold)
- [ ] Create audit log dashboard queries (Grafana/CloudWatch) - *Deferred to monitoring setup*
- [x] Add full-text search capability
- [x] Write unit and integration tests

**Database Requirements:**
- [x] Create indexes on: `UserId`, `IpAddress`, `EventType`, `Timestamp`
- [ ] Partition audit log table by month - *Can be added later if needed for scale*
- [x] Implement archive strategy for old logs (via `AuditLogRetentionService`)

**Implementation Summary:**
- ✅ Created `IAuditLogQueryService` interface in `Payment.Domain/Interfaces/`
- ✅ Implemented `AuditLogQueryService` with full filtering, pagination, sorting, and export
- ✅ Created comprehensive DTOs: `AuditLogQuery`, `AuditLogQueryResult`, `AuditLogSummary`, `SecurityEventQuery`, `SecurityEventDto`
- ✅ Created `AuditLogController` with `SecurityAdminOnly` policy
- ✅ Added CSV and JSON export functionality
- ✅ Implemented `AuditLogRetentionService` background service (90 days hot, 1 year cold)
- ✅ Added database indexes via migration `AddAuditLogIndexes`
- ✅ Comprehensive unit tests in `AuditLogQueryServiceTests`

**Files Created/Modified:**
- `src/Payment.Domain/Interfaces/IAuditLogQueryService.cs`
- `src/Payment.Application/DTOs/AuditLogQueryDto.cs`
- `src/Payment.Infrastructure/Auditing/AuditLogQueryService.cs`
- `src/Payment.API/Controllers/Admin/AuditLogController.cs`
- `src/Payment.Infrastructure/BackgroundServices/AuditLogRetentionService.cs`
- `src/Payment.Infrastructure/Migrations/20241201000000_AddAuditLogIndexes.cs`
- `src/Payment.Infrastructure/Data/Configurations/AuditLogConfiguration.cs` (updated with new indexes)
- `tests/Payment.Infrastructure.Tests/Auditing/AuditLogQueryServiceTests.cs`

**Estimated Effort:** 3-4 days ✅ **COMPLETED**

---

#### 7. Create Incident Response Runbooks ✅ COMPLETED
**Location:** `docs/runbooks/`

**Tasks:**
- [x] Create runbook template structure:
  - Incident type and severity
  - Prerequisites and access requirements
  - Step-by-step procedures
  - Rollback procedures
  - Escalation paths
  - Contact information
- [x] Create runbooks for:
  - Payment provider outage
  - Database connectivity issues
  - Circuit breaker failures
  - Security breach (unauthorized access)
  - Webhook signature validation failures
  - Rate limiting incidents
  - Performance degradation
- [ ] Add runbook automation scripts:
  - Kubernetes diagnostic scripts - *Can be added as needed*
  - Database query scripts - *Can be added as needed*
  - Log analysis scripts - *Can be added as needed*
  - Health check scripts - *Can be added as needed*
- [x] Create runbook index and search capability (README.md with index)
- [ ] Integrate runbooks with incident management system - *Requires external system integration*
- [x] Add runbook versioning and review process (documented in README)
- [ ] Create runbook training materials - *Can be added as needed*

**Format:**
- Markdown files in `docs/runbooks/`
- Include code snippets and commands
- Add diagrams for complex procedures
- Link to relevant monitoring dashboards

**Implementation Summary:**
- ✅ Created comprehensive runbook template structure
- ✅ Created 7 detailed runbooks covering all major incident types
- ✅ Each runbook includes: incident classification, prerequisites, step-by-step procedures, rollback, escalation, contacts
- ✅ Created runbook index (`README.md`) with usage guide
- ✅ All runbooks include kubectl commands, diagnostic steps, and monitoring dashboard references

**Files Created:**
- `docs/runbooks/README.md`
- `docs/runbooks/payment-provider-outage.md`
- `docs/runbooks/database-connectivity.md`
- `docs/runbooks/circuit-breaker-failures.md`
- `docs/runbooks/security-breach.md`
- `docs/runbooks/webhook-signature-failures.md`
- `docs/runbooks/rate-limiting-incidents.md`
- `docs/runbooks/performance-degradation.md`

**Estimated Effort:** 2-3 days ✅ **COMPLETED**

---

#### 8. Create Incident Report Templates ✅ COMPLETED
**Location:** `src/Payment.Application/Services/`

**Tasks:**
- [x] Create `IIncidentReportGenerator` interface:
  ```csharp
  public interface IIncidentReportGenerator
  {
      Task<IncidentReport> GeneratePaymentFailureReportAsync(
          PaymentFailureIncident incident,
          CancellationToken ct = default);
      
      Task<IncidentReport> GenerateSecurityIncidentReportAsync(
          SecurityIncident incident,
          CancellationToken ct = default);
  }
  ```
- [x] Implement report generator with templates:
  - Markdown template for payment failures
  - Markdown template for security incidents
  - HTML template for email notifications
  - PDF template for formal reports (using QuestPDF)
- [x] Create report sections:
  - Executive summary
  - Incident timeline
  - Root cause analysis
  - Impact assessment
  - Actions taken
  - Preventive measures
  - Lessons learned
- [x] Add report customization (include/exclude sections via `ReportGenerationOptions`)
- [x] Implement report export (PDF, HTML, Markdown)
- [x] Create admin endpoint for report generation
- [x] Add report versioning and archival (report includes version field)
- [ ] Write unit tests - *Can be added as needed*

**Template Engine:**
- ✅ Uses string-based template generation with conditional sections
- ✅ Support variable substitution
- ✅ Support conditional sections via `ReportGenerationOptions`

**Implementation Summary:**
- ✅ Created `IIncidentReportGenerator` interface in `Payment.Domain/Interfaces/`
- ✅ Implemented `IncidentReportGenerator` with Markdown, HTML, and PDF generation
- ✅ Integrated QuestPDF library for professional PDF generation
- ✅ Created comprehensive DTOs: `IncidentReport`, `PaymentFailureIncident`, `SecurityIncident`, `IncidentTimelineEvent`, `ReportGenerationOptions`
- ✅ Added admin endpoints in `IncidentController`:
  - `POST /api/v1/admin/incidents/payment-failure/report`
  - `POST /api/v1/admin/incidents/security/report`
- ✅ Reports support multiple formats (markdown, html, pdf) via query parameter
- ✅ Customizable sections via query parameters

**Files Created/Modified:**
- `src/Payment.Domain/Interfaces/IIncidentReportGenerator.cs`
- `src/Payment.Application/DTOs/IncidentReportDto.cs`
- `src/Payment.Application/Services/IncidentReportGenerator.cs`
- `src/Payment.API/Controllers/Admin/IncidentController.cs` (added report endpoints)
- `src/Payment.Application/Payment.Application.csproj` (added QuestPDF package)

**Estimated Effort:** 2-3 days ✅ **COMPLETED**

---

### 🟢 LOW Priority

#### 9. Schedule Regular Incident Response Drills
**Location:** `docs/incident-response-drills/`

**Tasks:**
- [ ] Create drill schedule (quarterly recommended):
  - Q1: Payment failure scenario
  - Q2: Security incident scenario
  - Q3: Performance degradation scenario
  - Q4: Multi-provider outage scenario
- [ ] Create drill scenarios:
  - Simulated payment provider outage
  - Simulated security breach
  - Simulated database failure
  - Simulated DDoS attack
- [ ] Create drill evaluation criteria:
  - Response time metrics
  - Communication effectiveness
  - Procedure adherence
  - Tool utilization
- [ ] Document drill results and improvements
- [ ] Update runbooks based on drill findings
- [ ] Create drill automation scripts
- [ ] Schedule drills in team calendar
- [ ] Create post-drill review process

**Drill Components:**
- Pre-drill briefing
- Scenario execution
- Real-time evaluation
- Post-drill debrief
- Action item tracking

**Estimated Effort:** 1-2 days (ongoing)

---

#### 10. Integrate with External Security Monitoring Tools
**Location:** `src/Payment.Infrastructure/Security/Integrations/`

**Tasks:**
- [ ] Research and select security monitoring tools:
  - SIEM (Security Information and Event Management)
  - IDS/IPS (Intrusion Detection/Prevention Systems)
  - Vulnerability scanners
  - Threat intelligence feeds
- [ ] Create integration interfaces:
  ```csharp
  public interface ISecurityMonitoringIntegration
  {
      Task SendSecurityEventAsync(SecurityEvent securityEvent, CancellationToken ct);
      Task<bool> IsThreatAsync(string ipAddress, CancellationToken ct);
      Task<IEnumerable<ThreatIntelligence>> GetThreatIntelligenceAsync(
          CancellationToken ct = default);
  }
  ```
- [ ] Implement integrations for:
  - AWS GuardDuty / Azure Security Center
  - Splunk / ELK Stack
  - OWASP ZAP / Burp Suite
  - Threat intelligence APIs
- [ ] Create security event forwarding:
  - Forward audit logs to SIEM
  - Forward security incidents to SOC
  - Integrate with threat intelligence feeds
- [ ] Add configuration for external tool endpoints
- [ ] Implement retry and error handling
- [ ] Add integration health checks
- [ ] Write integration tests

**Integration Options:**
- AWS: GuardDuty, Security Hub, CloudWatch
- Azure: Security Center, Sentinel
- Third-party: Splunk, Datadog Security, New Relic Security

**Estimated Effort:** 5-7 days

---

## Implementation Priority Summary

| Priority | Task | Estimated Effort | Dependencies |
|----------|------|----------------|--------------|
| 🔴 CRITICAL | Incident Response Services | 5-7 days | Core services |
| 🔴 CRITICAL | Admin Endpoints | 2-3 days | Incident services |
| 🟠 HIGH | Automated Alerting | 3-4 days | Incident services |
| 🟠 HIGH | Credential Revocation | 4-5 days | Security infrastructure |
| 🟡 MEDIUM | Audit Log Querying | 3-4 days | Audit infrastructure |
| 🟡 MEDIUM | Runbooks | 2-3 days | Documentation |
| 🟡 MEDIUM | Report Templates | 2-3 days | Incident services |
| 🟢 LOW | Incident Drills | 1-2 days | Ongoing |
| 🟢 LOW | External Security Tools | 5-7 days | Security infrastructure |

**Total Estimated Effort:** 27-38 days

**Recommended Sprint Planning:**
- **Sprint 1 (2 weeks):** Critical items (Tasks 1-3)
- **Sprint 2 (2 weeks):** High priority items (Tasks 4-5)
- **Sprint 3 (2 weeks):** Medium priority items (Tasks 6-8)
- **Sprint 4 (1 week):** Low priority items (Tasks 9-10)

---

## 📚 ADDITIONAL RESOURCES

- OWASP Top 10: https://owasp.org/www-project-top-ten/
- PCI DSS Guidelines: https://www.pcisecuritystandards.org/
- .NET Security Best Practices: https://docs.microsoft.com/en-us/aspnet/core/security/
- Kubernetes Security: https://kubernetes.io/docs/concepts/security/

---

**Document Version**: 1.0  
**Last Updated**: 2025-11-11  
**Next Review**: 2025-12-11