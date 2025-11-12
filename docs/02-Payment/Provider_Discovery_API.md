---
title: Provider Discovery API
version: 1.0
last_updated: 2025-01-27
category: Payment
tags:
  - payment
  - api
  - endpoints
  - providers
  - discovery
  - filtering
summary: >
  Provider Discovery API documentation for querying available payment providers
  with optional filtering by country, currency, and payment method.
related_docs:
  - Payment_Microservice.md
  - GraphQL_Support.md
  - ../01-Architecture/System_Architecture.md
ai_context_priority: high
---

# 🔍 Provider Discovery API

The **Provider Discovery API** provides a single global endpoint that allows clients (mobile apps, merchant dashboards, etc.) to query available payment providers based on country, currency, or payment method. This enables dynamic provider selection and discovery without hardcoding provider lists in client applications.

## 🎯 Features

- ✅ **Global Provider Discovery** - Single endpoint to discover all available payment providers
- ✅ **Flexible Filtering** - Filter by country, currency, or payment method (or any combination)
- ✅ **Case-Insensitive Matching** - Filters work regardless of case
- ✅ **Public Access** - No authentication required (AllowAnonymous)
- ✅ **Clean Architecture** - Follows CQRS pattern with MediatR
- ✅ **Stateless Design** - Kubernetes-ready, horizontally scalable
- ✅ **OpenTelemetry Tracing** - Full observability support
- ✅ **Configuration Support** - Can load providers from appsettings.json

## 📍 Endpoint

**Base URL:** `/api/v1/payments/providers`

**Method:** `GET`

**Authentication:** Not required (AllowAnonymous)

## 🔧 Query Parameters

All query parameters are optional. If no parameters are provided, the endpoint returns all active providers from all countries.

| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| `country` | string | No | ISO 3166-1 alpha-2 country code | `AE`, `IQ`, `KW` |
| `currency` | string | No | Currency code (ISO 4217) | `USD`, `AED`, `IQD` |
| `method` | string | No | Payment method type | `Card`, `Wallet` |

## 📊 Response Format

The endpoint returns an array of `PaymentProviderInfoDto` objects:

```typescript
interface PaymentProviderInfoDto {
  providerName: string;    // Provider name (e.g., "Paytabs", "Stripe")
  countryCode: string;     // ISO 3166-1 alpha-2 country code (e.g., "AE", "IQ")
  currency: string;        // Currency code (e.g., "USD", "AED", "IQD")
  paymentMethod: string;  // Payment method (e.g., "Card", "Wallet")
  isActive: boolean;       // Whether the provider is currently active
}
```

## 📝 API Examples

### 1. Get All Providers (No Filters)

Returns all active payment providers from all countries.

**Request:**
```http
GET /api/v1/payments/providers
```

**Response:**
```json
[
  {
    "providerName": "ZainCash",
    "countryCode": "IQ",
    "currency": "IQD",
    "paymentMethod": "Wallet",
    "isActive": true
  },
  {
    "providerName": "FIB",
    "countryCode": "IQ",
    "currency": "IQD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Paytabs",
    "countryCode": "AE",
    "currency": "AED",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Stripe",
    "countryCode": "US",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  }
]
```

### 2. Filter by Country

Get all providers available in a specific country.

**Request:**
```http
GET /api/v1/payments/providers?country=AE
```

**Response:**
```json
[
  {
    "providerName": "Telr",
    "countryCode": "AE",
    "currency": "AED",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Paytabs",
    "countryCode": "AE",
    "currency": "AED",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Tap",
    "countryCode": "AE",
    "currency": "AED",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "AmazonPaymentServices",
    "countryCode": "AE",
    "currency": "AED",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Checkout",
    "countryCode": "AE",
    "currency": "AED",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Stripe",
    "countryCode": "AE",
    "currency": "AED",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Verifone",
    "countryCode": "AE",
    "currency": "AED",
    "paymentMethod": "Card",
    "isActive": true
  }
]
```

### 3. Filter by Country and Payment Method

Get card providers in a specific country.

**Request:**
```http
GET /api/v1/payments/providers?country=IQ&method=Card
```

**Response:**
```json
[
  {
    "providerName": "FIB",
    "countryCode": "IQ",
    "currency": "IQD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "FIB",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Telr",
    "countryCode": "IQ",
    "currency": "IQD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Telr",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Paytabs",
    "countryCode": "IQ",
    "currency": "IQD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Paytabs",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Tap",
    "countryCode": "IQ",
    "currency": "IQD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Tap",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  }
]
```

### 4. Filter by Currency

Get all providers that support a specific currency.

**Request:**
```http
GET /api/v1/payments/providers?currency=USD
```

**Response:**
```json
[
  {
    "providerName": "ZainCash",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Wallet",
    "isActive": true
  },
  {
    "providerName": "FIB",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Telr",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Paytabs",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Tap",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  }
]
```

### 5. Filter by All Parameters

Get providers matching all criteria (country, currency, and payment method).

**Request:**
```http
GET /api/v1/payments/providers?country=IQ&currency=USD&method=Card
```

**Response:**
```json
[
  {
    "providerName": "FIB",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Telr",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Paytabs",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Tap",
    "countryCode": "IQ",
    "currency": "USD",
    "paymentMethod": "Card",
    "isActive": true
  }
]
```

### 6. No Results (Empty Response)

When no providers match the filters, an empty array is returned.

**Request:**
```http
GET /api/v1/payments/providers?country=XX
```

**Response:**
```json
[]
```

## 🔍 Filter Behavior

### Case-Insensitive Matching

All filters are case-insensitive. The following requests are equivalent:

```http
GET /api/v1/payments/providers?country=AE
GET /api/v1/payments/providers?country=ae
GET /api/v1/payments/providers?country=Ae
```

### Filter Combination Logic

Filters are combined using **AND** logic:
- All specified filters must match for a provider to be included
- If a filter parameter is omitted or empty, it is ignored (no filtering on that dimension)

**Examples:**
- `?country=IQ` → Returns all providers in Iraq
- `?country=IQ&method=Card` → Returns only card providers in Iraq
- `?country=IQ&currency=USD&method=Card` → Returns only USD card providers in Iraq

### Empty String Handling

Empty strings are treated as "no filter":
- `?country=` is equivalent to `?country` (no country filter)
- `?country=&currency=USD` filters only by currency

## 🏗️ Architecture

The Provider Discovery API follows **Clean Architecture** principles:

### Layers

1. **Domain Layer** (`Payment.Domain`)
   - `PaymentProviderCatalog` - Static catalog of providers
   - `PaymentProviderInfo` - Value object representing provider information
   - `GetAll()` method - Returns all active providers from all countries

2. **Application Layer** (`Payment.Application`)
   - `GetProvidersQuery` - CQRS query with optional filters
   - `GetProvidersHandler` - Query handler that filters providers
   - `PaymentProviderInfoDto` - DTO for API responses

3. **Presentation Layer** (`Payment.API`)
   - `PaymentsController.GetProviders()` - API endpoint
   - Uses MediatR to send queries to Application layer

### Flow Diagram

```
Client Request
    ↓
PaymentsController.GetProviders()
    ↓
MediatR.Send(GetProvidersQuery)
    ↓
GetProvidersHandler.Handle()
    ↓
PaymentProviderCatalog.GetAll()
    ↓
Filter by Country/Currency/Method
    ↓
Map to PaymentProviderInfoDto
    ↓
Return JSON Response
```

## ⚙️ Configuration

### Loading Providers from Configuration

Providers can be loaded from `appsettings.json` instead of using the default catalog:

```json
{
  "PaymentProviderCatalog": {
    "Providers": [
      {
        "ProviderName": "CustomProvider",
        "CountryCode": "US",
        "Currency": "USD",
        "PaymentMethod": "Card",
        "IsActive": true
      },
      {
        "ProviderName": "AnotherProvider",
        "CountryCode": "CA",
        "Currency": "CAD",
        "PaymentMethod": "Card",
        "IsActive": true
      }
    ]
  }
}
```

The handler automatically initializes the catalog from configuration if provided.

## 📊 Supported Countries

The default catalog includes providers for the following countries:

| Country Code | Country Name | Supported Currencies |
|--------------|--------------|---------------------|
| `IQ` | Iraq | IQD, USD |
| `KW` | Kuwait | KWD |
| `AE` | United Arab Emirates | AED |
| `SA` | Saudi Arabia | SAR |
| `BH` | Bahrain | BHD |
| `OM` | Oman | OMR |
| `QA` | Qatar | QAR |

## 🔒 Security Considerations

### Public Access

The endpoint is marked with `[AllowAnonymous]` to enable public access. This is intentional for provider discovery, as:
- No sensitive data is exposed (only provider names and capabilities)
- Enables client applications to dynamically discover available providers
- No authentication overhead for public-facing mobile apps

### Rate Limiting

Consider implementing rate limiting for this endpoint if it will receive high traffic:
- Use middleware to limit requests per IP
- Consider caching responses (providers don't change frequently)
- Monitor for abuse patterns

### Data Exposure

The endpoint only exposes:
- Provider names (public information)
- Country codes (public information)
- Currency codes (public information)
- Payment methods (public information)
- Active status (public information)

No sensitive data (API keys, credentials, transaction data) is exposed.

## 📈 Observability

### OpenTelemetry Tracing

The handler includes OpenTelemetry tracing with the following tags:
- `query.country` - Country filter value (or "all")
- `query.currency` - Currency filter value (or "all")
- `query.method` - Payment method filter value (or "all")
- `providers.count` - Number of providers returned

### Logging

Structured logging is included:
- **Information**: Provider count and filter values
- **Warning**: No providers found for filters
- **Error**: Exception details if query fails

### Metrics

Consider adding Prometheus metrics:
- `payment_providers_discovery_requests_total` - Total discovery requests
- `payment_providers_discovery_duration_seconds` - Request duration
- `payment_providers_discovery_results_count` - Number of providers returned

## 🧪 Testing

Comprehensive test coverage is available:

### Unit Tests
- **Application Layer**: `GetProvidersHandlerTests.cs` (15 test cases)
- **Domain Layer**: `PaymentProviderCatalogTests.cs` (5 test cases for `GetAll()`)

### Integration Tests
- **API Layer**: `GetProvidersTests.cs` (9 test cases)

### Test Coverage
- ✅ No filters (returns all providers)
- ✅ Single filters (country, currency, method)
- ✅ Multiple filters (combinations)
- ✅ Case-insensitive matching
- ✅ Empty results handling
- ✅ Configuration loading
- ✅ Inactive provider filtering

## 🚀 Usage Examples

### JavaScript/TypeScript

```typescript
// Get all providers in UAE
async function getUAEProviders() {
  const response = await fetch('/api/v1/payments/providers?country=AE');
  const providers = await response.json();
  return providers;
}

// Get card providers in Iraq
async function getIraqCardProviders() {
  const response = await fetch('/api/v1/payments/providers?country=IQ&method=Card');
  const providers = await response.json();
  return providers;
}

// Get all USD providers
async function getUSDProviders() {
  const response = await fetch('/api/v1/payments/providers?currency=USD');
  const providers = await response.json();
  return providers;
}
```

### cURL

```bash
# Get all providers
curl -X GET "https://api.example.com/api/v1/payments/providers"

# Get providers in UAE
curl -X GET "https://api.example.com/api/v1/payments/providers?country=AE"

# Get card providers in Iraq
curl -X GET "https://api.example.com/api/v1/payments/providers?country=IQ&method=Card"

# Get USD providers
curl -X GET "https://api.example.com/api/v1/payments/providers?currency=USD"
```

### C# / .NET

```csharp
// Using HttpClient
var client = new HttpClient();
var response = await client.GetAsync("https://api.example.com/api/v1/payments/providers?country=AE");
var providers = await response.Content.ReadFromJsonAsync<List<PaymentProviderInfoDto>>();

// Using RestSharp
var client = new RestClient("https://api.example.com");
var request = new RestRequest("/api/v1/payments/providers");
request.AddQueryParameter("country", "AE");
request.AddQueryParameter("method", "Card");
var providers = await client.GetAsync<List<PaymentProviderInfoDto>>(request);
```

## 🔄 Related Endpoints

### Get Providers by Country (Alternative)

For backward compatibility, a country-specific endpoint is also available:

**Endpoint:** `GET /api/v1/payments/providers/{countryCode}`

This endpoint returns providers for a specific country only. The Provider Discovery API (`/api/v1/payments/providers`) is more flexible and recommended for new integrations.

## 📚 Related Documentation

- [Payment Microservice API](Payment_Microservice.md) - Complete API documentation
- [System Architecture](../01-Architecture/System_Architecture.md) - Overall system design
- [GraphQL Support](GraphQL_Support.md) - GraphQL API documentation

## 🐛 Troubleshooting

### Empty Results

If you receive an empty array:
1. **Check country code**: Ensure it's a valid ISO 3166-1 alpha-2 code (2 characters)
2. **Check currency code**: Ensure it's a valid ISO 4217 currency code
3. **Check payment method**: Valid values are typically "Card" or "Wallet"
4. **Verify provider status**: Only active providers are returned

### Case Sensitivity

All filters are case-insensitive, but country codes in responses are always uppercase (e.g., "AE", "IQ").

### Performance

For high-traffic scenarios:
- Consider implementing response caching (providers don't change frequently)
- Use CDN caching for public endpoints
- Monitor query performance and optimize if needed

## 📝 Changelog

### Version 1.0 (2025-01-27)
- Initial release of Provider Discovery API
- Support for filtering by country, currency, and payment method
- Case-insensitive filter matching
- OpenTelemetry tracing support
- Configuration-based provider loading
- Comprehensive test coverage

