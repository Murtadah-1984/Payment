---
title: Regulatory Compliance
version: 1.0
last_updated: 2025-01-27
category: Payment
tags:
  - payment
  - compliance
  - regulatory
  - 3d-secure
  - cbk
  - sama
  - cbi
  - pci-dss
summary: >
  Regulatory Compliance feature for enforcing per-country compliance and regulation rules
  (e.g., CBK in Kuwait, SAMA in Saudi Arabia, PCI DSS, etc.).
related_docs:
  - Payment_Microservice.md
  - Security_Policy.md
  - ../01-Architecture/System_Architecture.md
ai_context_priority: high
---

# 🏛️ Regulatory Compliance

The **Regulatory Compliance** feature enforces per-country compliance and regulation rules to ensure all payment transactions adhere to local legal requirements. This feature validates payments against country-specific regulations before processing, preventing compliance violations and ensuring legal adherence across different regions.

## 🎯 Features

- ✅ **Per-Country Rule Enforcement** - Configurable compliance rules for each country
- ✅ **3D Secure Validation** - Automatic validation of 3D Secure requirements per country
- ✅ **Extensible Rules Engine** - Easy to add new compliance rules and validations
- ✅ **JSON Configuration** - Rules defined in JSON for easy updates without code changes
- ✅ **Clean Architecture** - Domain-driven design with clear separation of concerns
- ✅ **Stateless Design** - Kubernetes-ready, horizontally scalable
- ✅ **Comprehensive Logging** - Detailed logging of compliance violations
- ✅ **Graceful Fallback** - Continues processing if no rules are defined for a country

## 🧩 Architecture

The Regulatory Compliance feature follows Clean Architecture principles:

```
Domain Layer (Payment.Domain)
├─ Entities/
│   └─ ComplianceRule.cs          # Immutable record representing compliance rules
├─ Interfaces/
│   └─ IRegulatoryRulesEngine.cs  # Domain interface for compliance validation
├─ Services/
│   └─ RegulatoryRulesEngine.cs   # Rules engine implementation
└─ Exceptions/
    └─ ComplianceException.cs     # Custom exception for violations

Infrastructure Layer (Payment.Infrastructure)
├─ Config/
│   └─ ComplianceRules.json       # Configuration file with per-country rules
└─ DependencyInjection.cs        # Service registration and configuration loading

Application Layer (Payment.Application)
├─ DTOs/
│   └─ CreatePaymentDto.cs        # Added CountryCode parameter
└─ Services/
    └─ PaymentOrchestrator.cs     # Integrated compliance validation
```

## 📋 ComplianceRule Entity

The `ComplianceRule` is an immutable record that represents regulatory requirements for a specific country:

```csharp
public sealed record ComplianceRule(
    string CountryCode,              // ISO 3166-1 alpha-2 (e.g., "KW", "SA", "IQ")
    string RegulationName,           // Regulation name (e.g., "CBK", "SAMA", "CBI")
    string Description,              // Human-readable description
    bool Requires3DSecure,           // Whether 3D Secure is required
    bool RequiresEncryption,         // Whether encryption is required (future use)
    bool RequiresSettlementReport);  // Whether settlement reports are required (future use)
```

## 🔧 RegulatoryRulesEngine

The `RegulatoryRulesEngine` validates payment transactions against regulatory rules:

### Key Methods

- **`GetRule(string countryCode)`** - Retrieves the compliance rule for a country
- **`ValidateTransaction(string countryCode, Payment payment)`** - Validates a payment against country rules

### Validation Logic

1. If no rule exists for the country → **Allow transaction** (no restrictions)
2. If rule requires 3D Secure → **Check payment 3DS status**
   - ✅ Allow if `ThreeDSecureStatus == Authenticated` or `Skipped`
   - ❌ Reject if `ThreeDSecureStatus == NotRequired`, `Pending`, `ChallengeRequired`, or `Failed`
3. Future validations can be added for:
   - Encryption requirements
   - Settlement report requirements
   - Other country-specific rules

## 📁 Configuration

### ComplianceRules.json

Rules are defined in `src/Payment.Infrastructure/Config/ComplianceRules.json`:

```json
[
  {
    "CountryCode": "KW",
    "RegulationName": "CBK",
    "Description": "Central Bank of Kuwait requirements",
    "Requires3DSecure": true,
    "RequiresEncryption": true,
    "RequiresSettlementReport": true
  },
  {
    "CountryCode": "SA",
    "RegulationName": "SAMA",
    "Description": "Saudi Arabian Monetary Authority requirements",
    "Requires3DSecure": true,
    "RequiresEncryption": true,
    "RequiresSettlementReport": true
  },
  {
    "CountryCode": "IQ",
    "RegulationName": "CBI",
    "Description": "Central Bank of Iraq requirements",
    "Requires3DSecure": false,
    "RequiresEncryption": true,
    "RequiresSettlementReport": false
  }
]
```

### Configuration Loading

The system loads rules in the following order (later sources override earlier ones):

1. **JSON File** - `Config/ComplianceRules.json` (copied to output directory)
2. **appsettings.json** - `ComplianceRules` section
3. **Environment Variables** - Via configuration system

### Supported Countries

Currently configured countries:

| Country Code | Regulation | 3D Secure Required | Encryption Required | Settlement Report Required |
|--------------|------------|-------------------|---------------------|---------------------------|
| KW | CBK | ✅ Yes | ✅ Yes | ✅ Yes |
| SA | SAMA | ✅ Yes | ✅ Yes | ✅ Yes |
| IQ | CBI | ❌ No | ✅ Yes | ❌ No |
| AE | CBUAE | ✅ Yes | ✅ Yes | ✅ Yes |
| US | PCI DSS | ❌ No | ✅ Yes | ❌ No |

## 🔄 Integration with Payment Flow

Compliance validation is integrated into the `PaymentOrchestrator` workflow:

```csharp
// Step 4: Create payment entity
var payment = _paymentFactory.CreatePayment(request, splitPayment, metadata);

// Step 5: Validate regulatory compliance (if country code provided)
if (!string.IsNullOrWhiteSpace(request.CountryCode))
{
    var isValid = _regulatoryRulesEngine.ValidateTransaction(request.CountryCode, payment);
    if (!isValid)
    {
        var rule = _regulatoryRulesEngine.GetRule(request.CountryCode);
        throw new ComplianceException(
            $"Transaction violates {request.CountryCode} regulations ({rule?.RegulationName}).",
            request.CountryCode,
            rule?.RegulationName ?? "Unknown");
    }
}

// Step 6: Continue with payment processing...
```

### Validation Timing

Compliance validation occurs:
- ✅ **After** payment entity creation
- ✅ **Before** payment persistence
- ✅ **Before** payment processing

This ensures:
- Payment is rejected early if non-compliant
- No database records are created for non-compliant payments
- No provider API calls are made for non-compliant payments

## 📝 API Usage

### Create Payment with Country Code

Include the `countryCode` parameter in the payment request:

```http
POST /api/v1/payments
Content-Type: application/json
Authorization: Bearer {token}

{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "amount": 100.00,
  "currency": "USD",
  "paymentMethod": "CreditCard",
  "provider": "Tap",
  "merchantId": "merchant-123",
  "orderId": "order-456",
  "projectCode": "PROJECT-001",
  "idempotencyKey": "unique-key-12345",
  "countryCode": "KW"
}
```

### Response for Non-Compliant Payment

If the payment violates compliance rules:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "error": "ComplianceException",
  "message": "Transaction violates KW regulations (CBK).",
  "countryCode": "KW",
  "regulationName": "CBK"
}
```

## 🧪 Testing

### Unit Tests

**Location:** `tests/Payment.Domain.Tests/Services/RegulatoryRulesEngineTests.cs`

Tests cover:
- ✅ Rule retrieval by country code (case-insensitive)
- ✅ Validation with 3D Secure requirements
- ✅ Validation without 3D Secure requirements
- ✅ Null/empty country code handling
- ✅ Multiple rules handling
- ✅ Edge cases and error scenarios

### Integration Tests

**Location:** `tests/Payment.Application.Tests/Services/PaymentOrchestratorComplianceTests.cs`

Tests cover:
- ✅ Compliance validation when country code is provided
- ✅ No validation when country code is not provided
- ✅ Exception thrown when validation fails
- ✅ Processing continues when validation passes
- ✅ Empty country code handling

### Running Tests

```bash
# Run all compliance tests
dotnet test --filter "FullyQualifiedName~Compliance"

# Run domain tests
dotnet test tests/Payment.Domain.Tests/ --filter "FullyQualifiedName~RegulatoryRulesEngine"

# Run application tests
dotnet test tests/Payment.Application.Tests/ --filter "FullyQualifiedName~Compliance"
```

## 🔍 Example Scenarios

### Scenario 1: Kuwait Payment with 3D Secure

**Request:**
```json
{
  "amount": 100,
  "currency": "USD",
  "countryCode": "KW",
  "provider": "Tap"
}
```

**Flow:**
1. Payment entity created
2. Compliance validation checks CBK rules
3. Rule requires 3D Secure
4. Payment has `ThreeDSecureStatus = Authenticated`
5. ✅ Validation passes
6. Payment processing continues

### Scenario 2: Kuwait Payment without 3D Secure

**Request:**
```json
{
  "amount": 100,
  "currency": "USD",
  "countryCode": "KW",
  "provider": "Tap"
}
```

**Flow:**
1. Payment entity created
2. Compliance validation checks CBK rules
3. Rule requires 3D Secure
4. Payment has `ThreeDSecureStatus = NotRequired`
5. ❌ Validation fails
6. `ComplianceException` thrown
7. Payment rejected

### Scenario 3: Iraq Payment (No 3D Secure Required)

**Request:**
```json
{
  "amount": 100,
  "currency": "IQD",
  "countryCode": "IQ",
  "provider": "ZainCash"
}
```

**Flow:**
1. Payment entity created
2. Compliance validation checks CBI rules
3. Rule does not require 3D Secure
4. ✅ Validation passes (regardless of 3DS status)
5. Payment processing continues

### Scenario 4: Payment without Country Code

**Request:**
```json
{
  "amount": 100,
  "currency": "USD",
  "provider": "Stripe"
}
```

**Flow:**
1. Payment entity created
2. No compliance validation (country code not provided)
3. ✅ Payment processing continues

## 🚀 Adding New Compliance Rules

### Step 1: Add Rule to JSON Configuration

Edit `src/Payment.Infrastructure/Config/ComplianceRules.json`:

```json
{
  "CountryCode": "EG",
  "RegulationName": "CBE",
  "Description": "Central Bank of Egypt requirements",
  "Requires3DSecure": true,
  "RequiresEncryption": true,
  "RequiresSettlementReport": true
}
```

### Step 2: Update Documentation

Add the new country to the "Supported Countries" table in this document.

### Step 3: Add Tests

Add test cases for the new country in `RegulatoryRulesEngineTests.cs`.

## 🔐 Security Considerations

- **Configuration Security**: Compliance rules are loaded from configuration files. In production, ensure these files are:
  - ✅ Stored securely
  - ✅ Version controlled
  - ✅ Reviewed before deployment
  - ✅ Not exposed in public repositories

- **Validation Timing**: Compliance validation occurs before payment processing, preventing:
  - ❌ Non-compliant payments from being processed
  - ❌ Database records for invalid transactions
  - ❌ Unnecessary API calls to payment providers

- **Logging**: All compliance violations are logged with:
  - Payment ID
  - Country code
  - Regulation name
  - Timestamp

## 📊 Monitoring & Observability

### Logs

Compliance validation generates the following log entries:

**Successful Validation:**
```
[Information] Payment {PaymentId} passed compliance validation for {RegulationName} (Country: {CountryCode})
```

**Failed Validation:**
```
[Warning] Payment {PaymentId} rejected due to missing 3D Secure authentication (Regulation: {RegulationName}, Country: {CountryCode})
```

**No Rule Found:**
```
[Debug] No compliance rule found for country {CountryCode}, allowing transaction
```

### Metrics

Future enhancement: Add Prometheus metrics for:
- Compliance validation attempts
- Compliance validation failures
- Compliance validation by country
- Compliance validation by regulation

## 🔄 Future Enhancements

Potential enhancements to the Regulatory Compliance feature:

1. **Encryption Validation** - Validate that payment data is encrypted according to regulations
2. **Settlement Report Requirements** - Ensure settlement reports are generated when required
3. **Dynamic Rule Updates** - Support hot-reloading of compliance rules without restart
4. **Rule Versioning** - Track rule versions and changes over time
5. **Compliance Metrics** - Add Prometheus metrics for compliance monitoring
6. **Multi-Region Support** - Support for regional regulations (e.g., EU GDPR)
7. **Rule Templates** - Reusable rule templates for similar regulations
8. **Compliance Dashboard** - Admin dashboard for viewing and managing compliance rules

## 📚 Related Documentation

- [Payment Microservice](./Payment_Microservice.md) - Main payment service documentation
- [Security Policy](./Security_Policy.md) - Security and compliance policies
- [System Architecture](../01-Architecture/System_Architecture.md) - Overall system architecture
- [3D Secure Support](./TapToPay_Integration.md) - 3D Secure implementation details

## ✅ Summary

The Regulatory Compliance feature provides:

- ✅ **Per-country rule enforcement** for legal compliance
- ✅ **Automatic validation** of 3D Secure requirements
- ✅ **Extensible architecture** for adding new rules
- ✅ **JSON-based configuration** for easy updates
- ✅ **Comprehensive testing** with unit and integration tests
- ✅ **Clean Architecture** following SOLID principles
- ✅ **Stateless design** for Kubernetes deployment

This ensures all payment transactions comply with local regulations, preventing legal issues and maintaining regulatory adherence across different regions.

