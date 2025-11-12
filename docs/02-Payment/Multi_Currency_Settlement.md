---
title: Multi-Currency Settlement
version: 1.0
last_updated: 2025-01-15
category: Payment
tags:
  - settlement
  - currency
  - exchange-rate
  - multi-currency
summary: >
  Comprehensive documentation for multi-currency settlement feature including implementation,
  configuration, testing, and usage examples.
related_docs:
  - Payment_Microservice.md
  - Reporting_Module.md
ai_context_priority: high
---

# 💱 Multi-Currency Settlement

## Overview

The Multi-Currency Settlement feature enables automatic currency conversion when payments are completed. This allows the system to settle payments in a different currency than the payment currency, with full audit trail of exchange rates and conversion amounts.

## Architecture

### Clean Architecture Layers

The implementation follows Clean Architecture principles:

1. **Domain Layer** (`Payment.Domain`):
   - `ISettlementService` interface
   - `Payment` entity with settlement fields
   - `SetSettlement()` domain method

2. **Application Layer** (`Payment.Application`):
   - `SettlementService` implementation
   - Handler integration (`CompletePaymentCommandHandler`, `HandlePaymentCallbackCommandHandler`)
   - DTOs with settlement information

3. **Infrastructure Layer** (`Payment.Infrastructure`):
   - Database configuration
   - Dependency injection registration
   - Provider integration

## Implementation Details

### Domain Model

The `Payment` entity includes the following settlement fields:

```csharp
public Currency? SettlementCurrency { get; private set; }
public decimal? SettlementAmount { get; private set; }
public decimal? ExchangeRate { get; private set; }
public DateTime? SettledAt { get; private set; }
```

### Settlement Service

The `SettlementService` implements the following logic:

1. Checks if payment currency differs from settlement currency
2. If different, fetches exchange rate from `IExchangeRateService`
3. Converts payment amount to settlement currency
4. Calls `Payment.SetSettlement()` to store settlement information
5. Handles errors gracefully (non-blocking)

### Integration Points

Settlement is processed automatically when:

- Payment is completed via `CompletePaymentCommand`
- Payment is completed via callback (`HandlePaymentCallbackCommand`)

## Configuration

### appsettings.json

```json
{
  "Settlement": {
    "Currency": "USD",
    "Enabled": true
  },
  "Reporting": {
    "BaseCurrency": "USD"
  }
```

### Environment Variables

```bash
Settlement__Currency=USD
Settlement__Enabled=true
```

## Database Schema

The following fields are added to the `Payments` table:

- `SettlementCurrency` (VARCHAR(3), nullable)
- `SettlementAmount` (DECIMAL(18,2), nullable)
- `ExchangeRate` (DECIMAL(18,6), nullable)
- `SettledAt` (TIMESTAMP, nullable)

### Migration

```bash
dotnet ef migrations add AddMultiCurrencySettlement \
  --project src/Payment.Infrastructure \
  --startup-project src/Payment.API

dotnet ef database update \
  --project src/Payment.Infrastructure \
  --startup-project src/Payment.API
```

## API Usage

### Complete Payment with Settlement

```http
POST /api/v1/payments/{id}/complete
Authorization: Bearer {token}
Content-Type: application/json

{
  "settlementCurrency": "USD"
}
```

### Response Example

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 100.00,
  "currency": "EUR",
  "status": "Succeeded",
  "settlement": {
    "currency": "USD",
    "amount": 108.00,
    "exchangeRate": 1.08,
    "settledAt": "2024-01-15T10:30:00Z"
  }
}
```

## Testing

### Unit Tests

Comprehensive test coverage includes:

1. **SettlementServiceTests** (`tests/Payment.Application.Tests/Services/SettlementServiceTests.cs`):
   - Currency conversion scenarios
   - Same currency handling
   - Error handling
   - Multiple currency pairs
   - Exchange rate date handling

2. **PaymentSettlementTests** (`tests/Payment.Domain.Tests/Entities/PaymentSettlementTests.cs`):
   - `SetSettlement()` validation
   - Edge cases (zero amounts, negative rates)
   - Multiple settlement calls
   - Timestamp verification

3. **Handler Tests**:
   - `CompletePaymentCommandHandlerTests` with settlement
   - `HandlePaymentCallbackCommandHandlerTests` with settlement
   - Configuration fallback scenarios
   - Non-blocking error handling

### Running Tests

```bash
# Run all settlement tests
dotnet test --filter "FullyQualifiedName~Settlement"

# Run specific test class
dotnet test --filter "FullyQualifiedName~SettlementServiceTests"

# Run with coverage
dotnet test --filter "FullyQualifiedName~Settlement" /p:CollectCoverage=true
```

### Test Coverage

- ✅ Settlement service unit tests (100% coverage)
- ✅ Payment entity settlement method tests
- ✅ Handler integration tests
- ✅ Error handling and edge cases
- ✅ Configuration scenarios

## Error Handling

The settlement service implements graceful error handling:

1. **Exchange Rate Service Unavailable**: Payment completes without settlement
2. **Conversion Failure**: Payment completes without settlement
3. **Invalid Currency**: Payment completes without settlement
4. **Network Issues**: Payment completes without settlement

All errors are logged but do not block payment completion.

## Best Practices

1. **Always Configure Settlement Currency**: Set default in `appsettings.json`
2. **Monitor Exchange Rates**: Ensure `IExchangeRateService` is reliable
3. **Audit Trail**: Settlement information is stored for compliance
4. **Testing**: Test with various currency pairs
5. **Monitoring**: Monitor settlement success/failure rates

## Troubleshooting

### Settlement Not Processing

1. Check `Settlement:Enabled` configuration
2. Verify `IExchangeRateService` is registered
3. Check logs for conversion errors
4. Verify payment currency differs from settlement currency

### Incorrect Exchange Rates

1. Verify `IExchangeRateService` implementation
2. Check exchange rate source (API, database, etc.)
3. Review rate caching configuration
4. Check rate date/time accuracy

## Related Documentation

- [Payment Microservice](Payment_Microservice.md)
- [Reporting Module](Reporting_Module.md)
- [Testing Strategy](../04-Guidelines/Testing_Strategy.md)

