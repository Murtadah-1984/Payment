---
title: Tap-to-Pay Integration (NFC/HCE)
version: 2.0
last_updated: 2025-01-15
category: Payment
tags:
  - tap-to-pay
  - nfc
  - hce
  - apple-pay
  - google-pay
  - contactless
  - payment provider
  - integration
summary: >
  Comprehensive documentation for Tap-to-Pay integration using NFC/HCE tokens
  from Apple Pay, Google Pay, and Tap Company SDK. Includes setup, configuration,
  security, and usage examples.
related_docs:
  - Payment_Microservice.md
  - Security_Policy.md
  - ../03-Infrastructure/Observability.md
ai_context_priority: high
---

# 📱 Tap-to-Pay Integration (NFC/HCE)

The Payment Microservice includes **Tap-to-Pay** provider for processing contactless payments using NFC (Near Field Communication) or HCE (Host Card Emulation) tokens from mobile devices.

## Overview

Tap-to-Pay enables contactless payment processing through:
- **Apple Pay** - iOS devices with NFC capability
- **Google Pay** - Android devices with NFC capability  
- **Tap Company SDK** - Tap Payments mobile SDK

The mobile app collects tokenized payment data via NFC/HCE, and the Payment Microservice processes these tokens securely through the Tap Payments API.

## Architecture

```
Mobile App (Android/iOS)
 └─> TapToPay SDK (Google Pay / Apple Pay / Tap Co.)
      └─> NFC / EMV / HCE Token
           └─> REST call → Payment Microservice
                 └─> PaymentController
                      └─> PaymentOrchestrator
                           └─> TapToPayPaymentProvider
                                └─> Tap Payments API
```

## Features

- ✅ **NFC Token Processing**: Secure processing of tokenized NFC payment data
- ✅ **Replay Prevention**: Distributed cache (Redis) prevents token reuse across instances
- ✅ **PCI-DSS Compliance**: No raw card data stored, only tokenized payloads
- ✅ **Prometheus Metrics**: Comprehensive metrics for monitoring and alerting
- ✅ **Feature Flag Support**: Gradual rollout via `NewPaymentProvider` feature flag
- ✅ **Stateless Design**: K8S-ready with distributed cache for replay prevention
- ✅ **Error Handling**: Comprehensive error handling and retry logic
- ✅ **Security**: Token validation, replay prevention, and audit logging

## Configuration

### 1. Enable Feature Flag

Tap-to-Pay requires the `NewPaymentProvider` feature flag to be enabled:

```json
{
  "FeatureManagement": {
    "NewPaymentProvider": true
  }
}
```

### 2. Configure Tap-to-Pay Provider

Add Tap-to-Pay configuration to `appsettings.json`:

```json
{
  "PaymentProviders": {
    "TapToPay": {
      "BaseUrl": "https://api.tap.company/v2/",
      "SecretKey": "sk_test_...",
      "PublishableKey": "pk_test_...",
      "IsTestMode": true,
      "ReplayPreventionEnabled": true
    }
  },
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

**Configuration Options:**

| Option | Description | Required | Default |
|--------|-------------|----------|---------|
| `BaseUrl` | Tap Payments API base URL | Yes | `https://api.tap.company/v2/` |
| `SecretKey` | Tap Payments secret key (Bearer token) | Yes | - |
| `PublishableKey` | Tap Payments publishable key | No | - |
| `IsTestMode` | Enable test mode | No | `true` |
| `ReplayPreventionEnabled` | Enable distributed cache replay prevention | No | `true` |

**Production Configuration:**

For production, use environment variables or Kubernetes Secrets:

```yaml
# k8s/secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: payment-secrets
type: Opaque
stringData:
  PaymentProviders__TapToPay__SecretKey: "sk_live_..."
  PaymentProviders__TapToPay__PublishableKey: "pk_live_..."
  PaymentProviders__TapToPay__IsTestMode: "false"
```

### 3. Configure Redis for Replay Prevention

Tap-to-Pay uses Redis for distributed replay prevention. Configure Redis connection:

```json
{
  "ConnectionStrings": {
    "Redis": "redis-service:6379"
  }
}
```

**Kubernetes Configuration:**

```yaml
# k8s/configmap.yaml
data:
  ConnectionStrings__Redis: "redis-service.payment.svc.cluster.local:6379"
```

## Usage

### Mobile App Integration

#### Android (Google Pay / Tap SDK)

```kotlin
// Example: Collect NFC token from Google Pay
val paymentData = PaymentData.fromIntent(data)
val token = paymentData.paymentMethodToken.token

// Send to backend
val request = CreatePaymentRequest(
    amount = 100.0,
    currency = "IQD",
    paymentMethod = "TapToPay",
    provider = "TapToPay",
    merchantId = "MRC-001",
    orderId = "ORD-10001",
    nfcToken = token,
    deviceId = getDeviceId()
)
```

#### iOS (Apple Pay)

```swift
// Example: Collect NFC token from Apple Pay
let paymentToken = payment.token
let tokenString = String(data: paymentToken.paymentData, encoding: .utf8)

// Send to backend
let request = CreatePaymentRequest(
    amount: 100.0,
    currency: "IQD",
    paymentMethod: "TapToPay",
    provider: "TapToPay",
    merchantId: "MRC-001",
    orderId: "ORD-10001",
    nfcToken: tokenString,
    deviceId: getDeviceId()
)
```

### API Request

**Create Tap-to-Pay Payment:**

```http
POST /api/v1/payments
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "idempotencyKey": "tap-to-pay-order-456-20240115-001",
  "amount": 100.00,
  "currency": "IQD",
  "paymentMethod": "TapToPay",
  "provider": "TapToPay",
  "merchantId": "MRC-001",
  "orderId": "ORD-10001",
  "projectCode": "PROJECT-XYZ",
  "nfcToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "deviceId": "device-xyz-123",
  "customerId": "customer-456",
  "customerEmail": "customer@example.com",
  "customerPhone": "+9647501234567",
  "metadata": {
    "description": "Tap-to-Pay transaction",
    "customer_first_name": "John",
    "customer_last_name": "Doe"
  }
}
```

**Response:**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 100.00,
  "currency": "IQD",
  "paymentMethod": "TapToPay",
  "provider": "TapToPay",
  "merchantId": "MRC-001",
  "orderId": "ORD-10001",
  "status": "Completed",
  "transactionId": "chg_1234567890",
  "failureReason": null,
  "metadata": {
    "Provider": "TapToPay",
    "ChargeId": "chg_1234567890",
    "Status": "CAPTURED",
    "PaymentMethod": "TapToPay",
    "DeviceId": "device-xyz-123",
    "ProcessedAt": "2025-01-15T10:30:00.000Z"
  },
  "createdAt": "2025-01-15T10:30:00.000Z",
  "updatedAt": "2025-01-15T10:30:00.000Z"
}
```

## Security

### Replay Prevention

Tap-to-Pay implements **distributed replay prevention** using Redis:

- **Token Hashing**: NFC tokens are hashed using SHA-256 before storage
- **Distributed Cache**: Token hashes stored in Redis with 24-hour TTL
- **Cross-Instance Protection**: Prevents token reuse across multiple microservice instances
- **Automatic Expiry**: Tokens expire after 24 hours (configurable)

**Cache Key Format:**
```
tap_to_pay_token:{sha256_hash}
```

### PCI-DSS Compliance

- ✅ **No Raw Card Data**: Only tokenized NFC payloads are processed
- ✅ **Token Validation**: NFC tokens are validated before processing
- ✅ **Secure Storage**: Token hashes stored in distributed cache (not raw tokens)
- ✅ **HTTPS Only**: All API calls use HTTPS
- ✅ **Audit Logging**: All Tap-to-Pay transactions are logged with timestamps

### Best Practices

1. **Never Store NFC Tokens**: Tokens are single-use and ephemeral
2. **Use HTTPS**: Always use HTTPS for API communication
3. **Certificate Pinning**: Implement certificate pinning in mobile apps
4. **Token Validation**: Validate JWT signatures for Apple Pay/Google Pay tokens
5. **Secrets Management**: Store API keys in Azure Key Vault, AWS Secrets Manager, or K8s Secrets
6. **Monitor Replay Attempts**: Set up alerts for replay attempt metrics

## Prometheus Metrics

Tap-to-Pay exposes comprehensive Prometheus metrics:

### Available Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `payment_tap_to_pay_total` | Counter | `status` | Total Tap-to-Pay transactions (succeeded/failed) |
| `payment_tap_to_pay_failures_total` | Counter | `error_type` | Total failures by error type |
| `payment_tap_to_pay_duration_seconds` | Histogram | `status` | Transaction processing duration |
| `payment_tap_to_pay_replay_attempts_total` | Counter | - | Total detected replay attempts |
| `payment_tap_to_pay_last_transaction_timestamp` | Gauge | `status` | Unix timestamp of last transaction |

### Example Queries

**Success Rate:**
```promql
rate(payment_tap_to_pay_total{status="succeeded"}[5m]) / 
rate(payment_tap_to_pay_total[5m])
```

**Failure Rate:**
```promql
rate(payment_tap_to_pay_failures_total[5m])
```

**Average Duration:**
```promql
histogram_quantile(0.95, payment_tap_to_pay_duration_seconds)
```

**Replay Attempts:**
```promql
rate(payment_tap_to_pay_replay_attempts_total[5m])
```

### Grafana Dashboard

Create a Grafana dashboard with panels for:
- Transaction volume over time
- Success/failure rates
- P95/P99 latency
- Replay attempt alerts
- Error type breakdown

## Error Handling

### Common Errors

| Error | Description | Solution |
|-------|-------------|----------|
| `NFC token is required` | Missing `nfcToken` in request | Ensure mobile app sends tokenized NFC payload |
| `NFC token has already been processed` | Replay attack detected | Token was already used (replay prevention) |
| `TapToPay SecretKey must be configured` | Missing API credentials | Configure `PaymentProviders:TapToPay:SecretKey` |
| `NewPaymentProvider feature flag disabled` | Feature flag not enabled | Enable `NewPaymentProvider` in FeatureManagement |

### Error Response Example

```json
{
  "success": false,
  "transactionId": null,
  "failureReason": "NFC token has already been processed. Possible replay attack.",
  "providerMetadata": {
    "ReplayDetected": "true"
  }
}
```

## Testing

### Test Mode

Enable test mode in configuration:

```json
{
  "PaymentProviders": {
    "TapToPay": {
      "IsTestMode": true
    }
  }
}
```

### Test NFC Tokens

Use Tap Payments test tokens for development:

```json
{
  "nfcToken": "tok_test_1234567890"
}
```

### Integration Testing

```csharp
// Example integration test
[Fact]
public async Task ProcessTapToPayPayment_ShouldSucceed_WithValidNfcToken()
{
    var request = new CreatePaymentDto(
        RequestId: Guid.NewGuid(),
        Amount: 100.00m,
        Currency: "IQD",
        PaymentMethod: "TapToPay",
        Provider: "TapToPay",
        MerchantId: "MRC-001",
        OrderId: "ORD-TEST-001",
        ProjectCode: "TEST",
        IdempotencyKey: "test-key-001",
        NfcToken: "tok_test_valid_token",
        DeviceId: "test-device-001"
    );

    var result = await _orchestrator.ProcessPaymentAsync(request);
    
    Assert.True(result.Status == "Completed");
    Assert.NotNull(result.TransactionId);
}
```

## Deployment

### Kubernetes Deployment

1. **Enable Feature Flag:**
```yaml
# k8s/configmap.yaml
data:
  FeatureManagement__NewPaymentProvider: "true"
```

2. **Configure Secrets:**
```yaml
# k8s/secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: payment-secrets
stringData:
  PaymentProviders__TapToPay__SecretKey: "sk_live_..."
```

3. **Deploy Redis:**
```bash
kubectl apply -f k8s/redis-deployment.yaml
```

4. **Verify Deployment:**
```bash
# Check provider registration
curl https://payment-api/api/v1/payments/providers

# Should include "TapToPay" in response
```

### Health Checks

Tap-to-Pay provider is included in payment provider health checks:

```bash
curl https://payment-api/health/ready
```

## Monitoring & Alerts

### Recommended Alerts

1. **High Failure Rate:**
   - Alert when `payment_tap_to_pay_failures_total` exceeds 5% of total
   - Threshold: `rate(payment_tap_to_pay_failures_total[5m]) > 0.05`

2. **Replay Attempts:**
   - Alert when replay attempts detected
   - Threshold: `rate(payment_tap_to_pay_replay_attempts_total[5m]) > 0`

3. **High Latency:**
   - Alert when P95 latency exceeds 5 seconds
   - Threshold: `histogram_quantile(0.95, payment_tap_to_pay_duration_seconds) > 5`

4. **Provider Unavailable:**
   - Alert when provider health check fails
   - Check: `/health/ready` endpoint

## Troubleshooting

### Issue: "NFC token is required"

**Cause:** Missing `nfcToken` in request payload.

**Solution:** Ensure mobile app sends tokenized NFC payload in `nfcToken` field.

### Issue: "Replay attack detected"

**Cause:** NFC token has already been processed.

**Solution:** 
- Check if token was accidentally reused
- Verify Redis is accessible and working
- Check cache TTL configuration

### Issue: "NewPaymentProvider feature flag disabled"

**Cause:** Feature flag not enabled.

**Solution:** Enable `NewPaymentProvider` in `FeatureManagement` configuration.

### Issue: Redis connection failures

**Cause:** Redis service unavailable or misconfigured.

**Solution:**
- Verify Redis connection string
- Check Redis service health
- Review network connectivity

## See Also

- [Payment Microservice API](Payment_Microservice.md)
- [Security Policy](Security_Policy.md)
- [Observability & Metrics](../03-Infrastructure/Observability.md)
- [System Architecture](../01-Architecture/System_Architecture.md)
