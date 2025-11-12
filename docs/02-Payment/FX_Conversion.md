# 💱 FX Conversion Service

## Overview

The FX Conversion Service automatically converts between currencies in real-time when the payment currency differs from the provider's supported currency. This ensures seamless payment processing across different currency pairs without manual intervention.

## 🎯 Features

- **Automatic Currency Conversion**: Automatically detects currency mismatches and converts amounts
- **Real-Time Exchange Rates**: Uses live exchange rates from external Forex API
- **Provider-Aware**: Checks provider currency support before conversion
- **Graceful Fallback**: Continues with original currency if conversion fails
- **Comprehensive Logging**: Logs all conversion operations for audit and monitoring
- **Clean Architecture**: Follows SOLID principles and dependency inversion

## 🏗️ Architecture

### Layer Structure

```
Payment.Domain
 └─ Interfaces/
     └─ IForexApiClient.cs          # Domain abstraction for Forex API

Payment.Application
 └─ Services/
     ├─ IFxConversionService.cs     # Application service interface
     └─ FxConversionService.cs     # FX conversion orchestration
 └─ DTOs/
     └─ FxConversionResultDto.cs   # Conversion result DTO

Payment.Infrastructure
 └─ External/
     └─ ForexApiClient.cs           # External API implementation
```

### Dependency Flow

```
PaymentProcessingService
    ↓ (depends on)
IFxConversionService
    ↓ (depends on)
IForexApiClient (Domain)
    ↓ (implemented by)
ForexApiClient (Infrastructure)
```

## 📋 Components

### 1. IForexApiClient (Domain Interface)

**Location**: `src/Payment.Domain/Interfaces/IForexApiClient.cs`

Abstraction for external Forex API clients. Follows Dependency Inversion Principle.

```csharp
public interface IForexApiClient
{
    Task<(decimal Rate, decimal ConvertedAmount)> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        decimal amount,
        CancellationToken cancellationToken = default);
}
```

### 2. ForexApiClient (Infrastructure Implementation)

**Location**: `src/Payment.Infrastructure/External/ForexApiClient.cs`

Implementation using exchangerate.host API.

**Features**:
- HTTP client integration
- JSON response parsing
- Error handling and logging
- Configuration-based API key management
- Same currency optimization (returns rate 1.0)

**Configuration**:
```json
{
  "Forex": {
    "Provider": "exchangerate.host",
    "BaseUrl": "https://api.exchangerate.host",
    "ApiKey": "your_api_key_here"
  }
}
```

### 3. IFxConversionService (Application Interface)

**Location**: `src/Payment.Application/Services/IFxConversionService.cs`

Application service interface for currency conversion.

```csharp
public interface IFxConversionService
{
    Task<FxConversionResultDto> ConvertAsync(
        string fromCurrency,
        string toCurrency,
        decimal amount,
        CancellationToken cancellationToken = default);
}
```

### 4. FxConversionService (Application Implementation)

**Location**: `src/Payment.Application/Services/FxConversionService.cs`

Orchestrates currency conversion using the Forex API client.

**Responsibilities**:
- Validates input parameters
- Calls Forex API client
- Creates conversion result DTO
- Logs conversion operations
- Handles errors gracefully

### 5. FxConversionResultDto

**Location**: `src/Payment.Application/DTOs/FxConversionResultDto.cs`

Immutable record containing conversion results.

```csharp
public sealed record FxConversionResultDto(
    decimal OriginalAmount,
    decimal ConvertedAmount,
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateTime Timestamp);
```

## 🔄 Integration with Payment Processing

### PaymentProcessingService Integration

The FX conversion is automatically integrated into `PaymentProcessingService`:

1. **Currency Support Check**: Checks if provider supports payment currency using `PaymentProviderCatalog`
2. **Conversion Decision**: Converts only if provider doesn't support payment currency
3. **Primary Currency Selection**: Uses provider's primary currency from catalog
4. **Amount Conversion**: Converts payment amount using real-time exchange rate
5. **Request Preparation**: Creates payment request with converted amount and currency
6. **Error Handling**: Falls back to original currency if conversion fails

### Flow Diagram

```
Payment Processing Flow with FX Conversion
┌─────────────────────────────────────────┐
│ PaymentProcessingService                │
│                                          │
│ 1. Get payment currency                 │
│ 2. Check provider currency support       │
│    ├─ Supported? → No conversion        │
│    └─ Not supported? → Continue        │
│                                          │
│ 3. Get provider primary currency         │
│ 4. Call FxConversionService             │
│    ├─ Success? → Use converted amount   │
│    └─ Failure? → Use original currency  │
│                                          │
│ 5. Create PaymentRequest                 │
│ 6. Process through provider             │
└─────────────────────────────────────────┘
```

### Code Example

```csharp
// Inside PaymentProcessingService.ProcessPaymentAsync

var paymentCurrency = payment.Currency.Code;
var paymentAmount = payment.Amount.Value;

// Check if provider supports the payment currency
var providerSupportsCurrency = PaymentProviderCatalog.ProviderSupportsCurrency(
    provider.ProviderName,
    paymentCurrency);

// If provider doesn't support payment currency, convert
if (!providerSupportsCurrency && _fxConversionService != null)
{
    var providerPrimaryCurrency = PaymentProviderCatalog.GetProviderPrimaryCurrency(provider.ProviderName);
    
    if (!string.IsNullOrWhiteSpace(providerPrimaryCurrency) &&
        !string.Equals(paymentCurrency, providerPrimaryCurrency, StringComparison.OrdinalIgnoreCase))
    {
        var fxResult = await _fxConversionService.ConvertAsync(
            paymentCurrency,
            providerPrimaryCurrency,
            paymentAmount,
            cancellationToken);

        paymentAmount = fxResult.ConvertedAmount;
        paymentCurrency = providerPrimaryCurrency;
    }
}

// Create payment request with potentially converted amount/currency
var paymentRequest = new PaymentRequest(
    new Amount(paymentAmount),
    new Currency(paymentCurrency),
    payment.MerchantId,
    payment.OrderId,
    payment.SplitPayment,
    payment.Metadata);
```

## 🔧 Configuration

### appsettings.json

```json
{
  "Forex": {
    "Provider": "exchangerate.host",
    "BaseUrl": "https://api.exchangerate.host",
    "ApiKey": "your_api_key_here"
  }
}
```

### Environment Variables

```bash
Forex__BaseUrl=https://api.exchangerate.host
Forex__ApiKey=your_api_key_here
```

### Kubernetes Secrets

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: forex-api-key
type: Opaque
stringData:
  Forex__ApiKey: "your_api_key_here"
```

## 📊 PaymentProviderCatalog Extensions

New helper methods added to `PaymentProviderCatalog`:

### GetProviderCurrencies

Returns all supported currencies for a provider across all countries.

```csharp
var currencies = PaymentProviderCatalog.GetProviderCurrencies("Stripe");
// Returns: ["KWD", "AED", "SAR", "QAR", ...]
```

### ProviderSupportsCurrency

Checks if a provider supports a specific currency.

```csharp
var supports = PaymentProviderCatalog.ProviderSupportsCurrency("Stripe", "KWD");
// Returns: true
```

### GetProviderPrimaryCurrency

Gets the primary/default currency for a provider.

```csharp
var primaryCurrency = PaymentProviderCatalog.GetProviderPrimaryCurrency("ZainCash");
// Returns: "IQD" or "USD" (first supported currency)
```

## 🧪 Testing

### Unit Tests

Comprehensive test coverage for all components:

- **FxConversionServiceTests**: Tests conversion logic, error handling, edge cases
- **ForexApiClientTests**: Tests API integration, response parsing, error scenarios
- **PaymentProcessingServiceFxTests**: Tests integration with payment processing
- **PaymentProviderCatalogTests**: Tests new currency-related methods

### Test Examples

```csharp
[Fact]
public async Task ConvertAsync_ShouldReturnConversionResult_WhenConversionSucceeds()
{
    // Arrange
    var fxResult = await _fxService.ConvertAsync("USD", "EUR", 100m);
    
    // Assert
    fxResult.ConvertedAmount.Should().Be(85m);
    fxResult.Rate.Should().Be(0.85m);
}
```

## 📝 Usage Examples

### Example 1: Automatic Conversion

**Scenario**: Payment in EUR, Provider supports only USD

```csharp
// Payment: EUR 100.00
// Provider: ZainCash (supports IQD, USD)
// Result: Automatically converts EUR → USD at current rate
```

### Example 2: No Conversion Needed

**Scenario**: Payment in KWD, Provider supports KWD

```csharp
// Payment: KWD 100.00
// Provider: Stripe (supports KWD)
// Result: No conversion, processes directly
```

### Example 3: Conversion Failure Fallback

**Scenario**: FX API unavailable, payment continues with original currency

```csharp
// Payment: EUR 100.00
// Provider: ZainCash
// FX API: Unavailable
// Result: Processes EUR 100.00 directly (provider may accept it)
```

## 🚨 Error Handling

### Conversion Failures

When FX conversion fails, the service:
1. Logs the error with full context
2. Continues with original currency
3. Allows provider to process (some providers accept multiple currencies)

### Error Scenarios

- **API Unavailable**: Falls back to original currency
- **Invalid Currency Pair**: Throws `InvalidOperationException`
- **Network Timeout**: Falls back to original currency
- **Invalid Response**: Throws `InvalidOperationException` with parsing error

## 📈 Monitoring & Logging

### Log Events

All conversion operations are logged:

```
[Information] Converting 100.00 from EUR to USD
[Information] FX conversion completed: 100.00 EUR → 110.00 USD (Rate: 1.10)
[Error] Failed to convert currency from EUR to USD for payment {PaymentId}
```

### Metrics (Future Enhancement)

Consider adding metrics for:
- Conversion success/failure rates
- Conversion latency
- Exchange rate volatility
- Provider currency mismatch frequency

## 🔒 Security Considerations

1. **API Key Management**: Store API keys in secrets management (K8s Secrets, Azure Key Vault, AWS Secrets Manager)
2. **Rate Limiting**: Implement rate limiting for Forex API calls
3. **Input Validation**: All currency codes and amounts are validated
4. **Error Information**: Don't expose sensitive API details in error messages

## 🚀 Deployment

### Docker

No special configuration needed. FX service is automatically registered via dependency injection.

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: payment-service
spec:
  template:
    spec:
      containers:
      - name: payment-api
        env:
        - name: Forex__BaseUrl
          value: "https://api.exchangerate.host"
        - name: Forex__ApiKey
          valueFrom:
            secretKeyRef:
              name: forex-api-key
              key: Forex__ApiKey
```

## 🔄 Future Enhancements

1. **Caching**: Cache exchange rates for short periods to reduce API calls
2. **Multiple Providers**: Support multiple Forex API providers with fallback
3. **Rate History**: Store historical rates for reporting and analytics
4. **Scheduled Updates**: Pre-fetch rates for common currency pairs
5. **Margin Management**: Add configurable margins for conversion
6. **Currency Validation**: Validate currency codes against ISO 4217 standard

## 📚 Related Documentation

- [Multi-Currency Settlement](./Multi_Currency_Settlement.md)
- [Payment Processing](./Payment_Microservice.md)
- [Provider Discovery API](./Provider_Discovery_API.md)

## 🐛 Troubleshooting

### Conversion Not Happening

1. Check if provider supports payment currency: `PaymentProviderCatalog.ProviderSupportsCurrency()`
2. Verify FX service is registered: Check `DependencyInjection.cs`
3. Check logs for conversion attempts

### API Errors

1. Verify API key is configured correctly
2. Check network connectivity to Forex API
3. Review API response format (may have changed)

### Incorrect Conversion Rates

1. Verify API is returning correct rates
2. Check for API rate limits
3. Consider implementing rate caching

## 📞 Support

For issues or questions:
1. Check application logs for detailed error messages
2. Review Forex API documentation
3. Contact development team with payment ID and timestamp

