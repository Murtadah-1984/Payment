---
title: Extension Guide
version: 1.0
last_updated: 2025-11-11
category: Guidelines
tags:
  - extension
  - payment providers
  - factory pattern
  - strategy pattern
summary: >
  Guide for extending the Payment Microservice with new payment providers,
  following Factory + Strategy pattern and Clean Architecture principles.
related_docs:
  - Coding_Standards.md
  - ../01-Architecture/System_Architecture.md
ai_context_priority: high
---

# 🔧 Extension Guide

## Adding a New Payment Provider

The Payment Microservice uses a **Factory + Strategy pattern** to support multiple payment providers. Follow these steps to add a new provider:

### Step 1: Implement Domain Interface

Create a new class implementing `IPaymentProvider` in the Infrastructure layer:

```csharp
// src/Payment.Infrastructure/Providers/NewProvider.cs
public class NewProvider : IPaymentProvider
{
    public string ProviderName => "NewProvider";

    public async Task<PaymentResult> ProcessPaymentAsync(
        PaymentRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Implement payment processing logic
        // Call external API, handle responses, etc.
        
        return new PaymentResult(
            Success: true,
            TransactionId: "txn_123456",
            FailureReason: null,
            ProviderMetadata: new Dictionary<string, string>());
    }
}
```

### Step 2: Implement Callback Interface (Optional)

If the provider supports callbacks/webhooks, implement `IPaymentCallbackProvider`:

```csharp
public class NewProvider : IPaymentProvider, IPaymentCallbackProvider
{
    // ... IPaymentProvider implementation ...

    public async Task<PaymentResult> VerifyCallbackAsync(
        Dictionary<string, string> callbackData,
        CancellationToken cancellationToken = default)
    {
        // Verify callback signature
        // Extract payment information
        // Return payment result
        
        return new PaymentResult(
            Success: true,
            TransactionId: callbackData["transaction_id"],
            FailureReason: null,
            ProviderMetadata: callbackData);
    }
}
```

### Step 3: Register Provider

Register the provider in `Program.cs`:

```csharp
// src/Payment.API/Program.cs
builder.Services.AddScoped<IPaymentProvider, NewProvider>();
```

### Step 4: Add Configuration

Add provider configuration to `appsettings.json`:

```json
{
  "PaymentProviders": {
    "NewProvider": {
      "ApiKey": "your-api-key",
      "MerchantId": "your-merchant-id",
      "WebhookSecret": "your-webhook-secret",
      "BaseUrl": "https://api.newprovider.com",
      "IsEnabled": true
    }
  }
}
```

### Step 5: Add Callback Endpoint (Optional)

If the provider supports callbacks, add a callback endpoint:

```csharp
// src/Payment.API/Controllers/PaymentsController.cs
[HttpPost("newprovider/callback")]
[AllowAnonymous] // Callbacks don't require authentication
public async Task<IActionResult> HandleNewProviderCallback(
    [FromBody] Dictionary<string, string> callbackData,
    CancellationToken cancellationToken)
{
    var command = new HandlePaymentCallbackCommand(
        Provider: "NewProvider",
        CallbackData: callbackData);
    
    var result = await _mediator.Send(command, cancellationToken);
    return Ok(result);
}
```

### Step 6: Update Validator

Add the new provider to the validator whitelist:

```csharp
// src/Payment.Application/Validators/CreatePaymentCommandValidator.cs
RuleFor(x => x.Provider)
    .Must(provider => new[] { 
        "ZainCash", "Stripe", "NewProvider", // Add here
        // ... other providers
    }.Contains(provider, StringComparison.OrdinalIgnoreCase))
    .WithMessage("Invalid payment provider");
```

### Step 7: Add Tests

Create unit tests for the new provider:

```csharp
// tests/Payment.Infrastructure.Tests/Providers/NewProviderTests.cs
public class NewProviderTests
{
    [Fact]
    public async Task ProcessPaymentAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var provider = new NewProvider();
        var request = new PaymentRequest(...);

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TransactionId);
    }
}
```

## Best Practices

1. **Follow Clean Architecture**: Keep provider implementations in Infrastructure layer
2. **Use Dependency Injection**: Inject configuration, HTTP clients, loggers
3. **Handle Errors Gracefully**: Return appropriate error messages
4. **Implement Retry Logic**: Use Polly for resilience (already configured)
5. **Validate Callbacks**: Always validate webhook signatures
6. **Log Operations**: Use structured logging for all operations
7. **Write Tests**: Comprehensive unit and integration tests

## See Also

- [Coding Standards](Coding_Standards.md)
- [System Architecture](../01-Architecture/System_Architecture.md)
- [Payment Microservice](../02-Payment/Payment_Microservice.md)

