---
title: Payment Microservice
version: 1.0
last_updated: 2025-11-11
category: Payment
tags:
  - payment
  - api
  - endpoints
  - providers
  - status-flow
summary: >
  Complete Payment Microservice API documentation including endpoints, request/response examples,
  payment status flow, and provider integration details.
related_docs:
  - Reporting_Module.md
  - Security_Policy.md
  - GraphQL_Support.md
  - ../01-Architecture/System_Architecture.md
ai_context_priority: high
---

# 💳 Payment Microservice API Documentation

Once running, access:
- **Swagger UI**: `https://localhost:5001/swagger` or `http://localhost:5000/swagger`
- **GraphQL IDE**: `https://localhost:5001/graphql` (development only)

For GraphQL API documentation, see [GraphQL Support](GraphQL_Support.md).

For Provider Discovery API documentation, see [Provider Discovery API](Provider_Discovery_API.md).

## Available Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `GET` | `/api/v1/payments/providers` | Provider Discovery API - Get providers with optional filtering (country, currency, method) | No |
| `POST` | `/api/v1/payments` | Create a new payment | Yes |
| `GET` | `/api/v1/payments/{id}` | Get payment by ID | Yes |
| `GET` | `/api/v1/payments/order/{orderId}` | Get payment by order ID | Yes |
| `GET` | `/api/v1/payments/merchant/{merchantId}` | Get payments by merchant | Yes |
| `POST` | `/api/v1/payments/{id}/process` | Mark payment as processing | Yes |
| `POST` | `/api/v1/payments/{id}/complete` | Mark payment as completed | Yes |
| `POST` | `/api/v1/payments/{id}/fail` | Mark payment as failed | Yes |
| `POST` | `/api/v1/payments/{id}/refund` | Refund a completed payment | Yes |
| `POST/GET` | `/api/v1/payments/zaincash/callback` | ZainCash payment callback | No |
| `POST` | `/api/v1/payments/fib/callback` | FIB payment callback | No |
| `POST/GET` | `/api/v1/payments/telr/callback` | Telr payment callback | No |
| `POST` | `/api/v1/payments/{id}/3ds/initiate` | Initiate 3D Secure authentication | Yes |
| `POST/GET` | `/api/v1/payments/{id}/3ds/callback` | Complete 3D Secure authentication | No |
| `GET` | `/health` | Health check endpoint | No |
| `GET` | `/ready` | Readiness probe endpoint | No |
| `GET` | `/metrics` | Prometheus metrics endpoint | No |
| `POST` | `/graphql` | GraphQL API endpoint | Yes |
| `GET` | `/graphql` | GraphQL IDE (development only) | No |

## Sample API Requests

### 1. Create Payment (Simple Split)

**Request:**
```http
POST /api/v1/payments
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "idempotencyKey": "payment-order-456-20240115-001",
  "amount": 100.00,
  "currency": "USD",
  "paymentMethod": "CreditCard",
  "provider": "Stripe",
  "merchantId": "merchant-123",
  "orderId": "order-456",
  "projectCode": "PROJECT-XYZ",
  "systemFeePercent": 5.0,
  "customerEmail": "customer@example.com",
  "customerPhone": "+1234567890",
  "metadata": {
    "projectId": "XYZ123",
    "userId": "user-789"
  }
}
```

**Response:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 100.00,
  "currency": "USD",
  "paymentMethod": "CreditCard",
  "provider": "Stripe",
  "merchantId": "merchant-123",
  "orderId": "order-456",
  "status": "Processing",
  "transactionId": "txn_1234567890",
  "failureReason": null,
  "splitPayment": {
    "systemShare": 5.00,
    "ownerShare": 95.00,
    "systemFeePercent": 5.0
  },
  "metadata": {
    "projectId": "XYZ123",
    "userId": "user-789",
    "project_code": "PROJECT-XYZ",
    "request_id": "550e8400-e29b-41d4-a716-446655440000",
    "customer_email": "customer@example.com",
    "customer_phone": "+1234567890"
  },
  "createdAt": "2024-01-15T10:30:00Z",
  "updatedAt": "2024-01-15T10:30:00Z"
}
```

### 2. Create Payment (Multi-Account Split)

**Request:**
```http
POST /api/v1/payments
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "requestId": "660e8400-e29b-41d4-a716-446655440001",
  "idempotencyKey": "payment-order-789-20240115-002",
  "amount": 1000.00,
  "currency": "USD",
  "paymentMethod": "Wallet",
  "provider": "ZainCash",
  "merchantId": "merchant-123",
  "orderId": "order-789",
  "projectCode": "PROJECT-ABC",
  "splitRule": {
    "systemFeePercent": 10.0,
    "accounts": [
      {
        "accountType": "SystemOwner",
        "accountIdentifier": "system-wallet-001",
        "percentage": 10.0
      },
      {
        "accountType": "ServiceOwner",
        "accountIdentifier": "service-wallet-002",
        "percentage": 60.0
      },
      {
        "accountType": "Partner",
        "accountIdentifier": "partner-wallet-003",
        "percentage": 30.0
      }
    ]
  },
  "customerPhone": "+962791234567",
  "metadata": {
    "projectId": "ABC123"
  }
}
```

**Response:**
```json
{
  "id": "4fa85f64-5717-4562-b3fc-2c963f66afb7",
  "amount": 1000.00,
  "currency": "USD",
  "paymentMethod": "Wallet",
  "provider": "ZainCash",
  "merchantId": "merchant-123",
  "orderId": "order-789",
  "status": "Processing",
  "transactionId": "zain_txn_9876543210",
  "failureReason": null,
  "splitPayment": {
    "systemShare": 100.00,
    "ownerShare": 900.00,
    "systemFeePercent": 10.0
  },
  "metadata": {
    "projectId": "ABC123",
    "project_code": "PROJECT-ABC",
    "request_id": "660e8400-e29b-41d4-a716-446655440001",
    "customer_phone": "+962791234567",
    "split_details": "{\"SplitRule\":{\"SystemFeePercent\":10.0,\"Accounts\":[{\"AccountType\":\"SystemOwner\",\"AccountIdentifier\":\"system-wallet-001\",\"Percentage\":10.0,\"Amount\":100.00},{\"AccountType\":\"ServiceOwner\",\"AccountIdentifier\":\"service-wallet-002\",\"Percentage\":60.0,\"Amount\":600.00},{\"AccountType\":\"Partner\",\"AccountIdentifier\":\"partner-wallet-003\",\"Percentage\":30.0,\"Amount\":300.00}]},\"TotalAmount\":1000.00,\"SystemShare\":100.00,\"OwnerShare\":900.00}"
  },
  "createdAt": "2024-01-15T11:00:00Z",
  "updatedAt": "2024-01-15T11:00:00Z"
}
```

### 3. Get Payment by ID

**Request:**
```http
GET /api/v1/payments/3fa85f64-5717-4562-b3fc-2c963f66afa6
Authorization: Bearer {your-jwt-token}
```

**Response:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 100.00,
  "currency": "USD",
  "paymentMethod": "CreditCard",
  "provider": "Stripe",
  "merchantId": "merchant-123",
  "orderId": "order-456",
  "status": "Completed",
  "transactionId": "txn_1234567890",
  "failureReason": null,
  "splitPayment": {
    "systemShare": 5.00,
    "ownerShare": 95.00,
    "systemFeePercent": 5.0
  },
  "metadata": {
    "projectId": "XYZ123"
  },
  "createdAt": "2024-01-15T10:30:00Z",
  "updatedAt": "2024-01-15T10:35:00Z"
}
```

### 6. Get Payment by Order ID

**Request:**
```http
GET /api/v1/payments/order/order-456
Authorization: Bearer {your-jwt-token}
```

### 7. Get Payments by Merchant

**Request:**
```http
GET /api/v1/payments/merchant/merchant-123
Authorization: Bearer {your-jwt-token}
```

**Response:**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "amount": 100.00,
    "currency": "USD",
    "paymentMethod": "CreditCard",
    "provider": "Stripe",
    "merchantId": "merchant-123",
    "orderId": "order-456",
    "status": "Completed",
    "transactionId": "txn_1234567890",
    "failureReason": null,
    "splitPayment": {
      "systemShare": 5.00,
      "ownerShare": 95.00,
      "systemFeePercent": 5.0
    },
    "metadata": {},
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T10:35:00Z"
  }
]
```

### 8. Complete Payment

**Request:**
```http
POST /api/v1/payments/3fa85f64-5717-4562-b3fc-2c963f66afa6/complete
Authorization: Bearer {your-jwt-token}
```

### 9. Fail Payment

**Request:**
```http
POST /api/v1/payments/3fa85f64-5717-4562-b3fc-2c963f66afa6/fail
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "reason": "Insufficient funds"
}
```

### 10. Refund Payment

**Request:**
```http
POST /api/v1/payments/3fa85f64-5717-4562-b3fc-2c963f66afa6/refund
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "refundTransactionId": "refund_txn_9876543210"
}
```

### 11. Get Available Providers

**Request:**
```http
GET /api/v1/payments/providers
```

**Response:**
```json
[
  "ZainCash",
  "AsiaHawala",
  "Stripe",
  "FIB",
  "Square",
  "Helcim",
  "AmazonPaymentServices",
  "Telr",
  "Checkout",
  "Verifone",
  "Paytabs",
  "Tap",
  "TapToPay"
]
```

### 12. Initiate 3D Secure Authentication

Initiates 3D Secure authentication for a payment that requires additional cardholder verification.

**Request:**
```http
POST /api/v1/payments/{id}/3ds/initiate
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "returnUrl": "https://example.com/payment/return"
}
```

**Response (3DS Required):**
```json
{
  "acsUrl": "https://acs.example.com/authenticate",
  "pareq": "base64-encoded-pareq-data",
  "md": "merchant-data-123",
  "termUrl": "https://api.payment.com/api/v1/payments/{id}/3ds/callback",
  "version": "2.2.0"
}
```

**Response (3DS Not Required):**
```json
{
  "message": "3D Secure authentication not required for this payment"
}
```

**Notes:**
- The client should redirect the user to `acsUrl` with `pareq` and `md` parameters
- After authentication, the ACS will redirect to `termUrl` with authentication results
- Payment must have a card token to initiate 3DS
- Not all payments require 3DS (determined by provider, amount, currency, and card issuer)

### 13. Complete 3D Secure Authentication

Completes 3D Secure authentication after the user authenticates on the ACS (Access Control Server). Supports both POST (JSON body) and GET (query parameters) for ACS redirects.

**Request (POST):**
```http
POST /api/v1/payments/{id}/3ds/callback
Content-Type: application/json

{
  "pareq": "base64-encoded-pareq",
  "ares": "base64-encoded-ares",
  "md": "merchant-data-123"
}
```

**Request (GET - ACS Redirect):**
```http
GET /api/v1/payments/{id}/3ds/callback?pareq=base64-pareq&ares=base64-ares&md=merchant-data-123
```

**Response (Success):**
```json
{
  "authenticated": true,
  "cavv": "cavv-1234567890123456789012345678",
  "eci": "05",
  "xid": "xid-12345678901234567890",
  "version": "2.2.0",
  "failureReason": null
}
```

**Response (Failure):**
```json
{
  "authenticated": false,
  "cavv": null,
  "eci": null,
  "xid": null,
  "version": null,
  "failureReason": "Authentication failed"
}
```

**Notes:**
- This endpoint is `[AllowAnonymous]` to allow ACS callbacks, but should be validated
- POST body takes precedence over query parameters if both are provided
- After successful authentication, the payment can proceed to completion
- CAVV (Cardholder Authentication Verification Value) is stored for liability shift

## 💳 Payment Status Flow

The payment lifecycle follows this state machine:

```
Pending → Processing → Completed
   ↓           ↓
Failed      Failed
   ↑
Refunded ← Completed
```

**Status Values:**
- `Pending` (0): Payment created, awaiting processing
- `Processing` (1): Payment being processed by provider
- `Completed` (2): Payment successfully completed
- `Failed` (3): Payment failed (with reason)
- `Refunded` (4): Payment refunded
- `Cancelled` (5): Payment cancelled

For detailed state machine implementation, see [State Machine Documentation](../03-Infrastructure/Performance_Optimization.md#state-machine-for-payment-status).

## Payment Providers

The Payment Microservice supports 13 payment providers:

1. **ZainCashPaymentProvider** - Middle East wallet payments
2. **AsiaHawalaPaymentProvider** - Hawala payment system
3. **StripePaymentProvider** - Global card payments (with 3D Secure support)
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

### 3D Secure Support

Some payment providers support 3D Secure (3DS) authentication for enhanced card payment security. Providers that support 3DS implement the `IThreeDSecurePaymentProvider` interface in addition to `IPaymentProvider`.

**Providers with 3DS Support:**
- **StripePaymentProvider** - Full 3DS 2.2.0 support

**3DS Flow:**
1. Create payment with card token
2. Call `/api/v1/payments/{id}/3ds/initiate` to check if 3DS is required
3. If required, redirect user to ACS URL with challenge data
4. User authenticates on ACS
5. ACS redirects to `/api/v1/payments/{id}/3ds/callback` with authentication result
6. Payment proceeds to completion if authentication successful

**3DS Status Values:**
- `NotRequired` - 3DS not required for this payment
- `ChallengeRequired` - 3DS challenge initiated, awaiting user authentication
- `Authenticated` - 3DS authentication successful
- `Failed` - 3DS authentication failed
- `Skipped` - 3DS was skipped (not required or not applicable)

## Webhook Notifications

The Payment Microservice automatically sends webhooks to external systems when payment status changes occur. The system includes a robust retry mechanism with exponential backoff to ensure reliable delivery.

### Features

- Automatic webhook delivery on payment status changes
- Exponential backoff retry mechanism (1s, 2s, 4s, 8s, etc.)
- Configurable maximum retry attempts (default: 5)
- Multiple webhook URL resolution sources:
  - Payment metadata (`webhook_url`, `webhookUrl`, `callback_url`, `callbackUrl`)
  - Merchant-specific configuration
  - Default webhook URL
- Background service for processing failed webhook deliveries
- Comprehensive logging and observability

For detailed documentation, see [Webhook Retry Mechanism](Webhook_Retry_Mechanism.md).

## Multi-Currency Settlement

The Payment Microservice supports automatic currency conversion for multi-currency settlement. When a payment is completed, if the payment currency differs from the configured settlement currency, the system automatically converts the amount and stores the settlement information.

### Features

- ✅ **Automatic Currency Conversion**: Converts payment amounts to settlement currency when payment completes
- ✅ **Exchange Rate Tracking**: Stores the exchange rate used for conversion with each payment
- ✅ **Audit Trail**: Settlement amount, currency, exchange rate, and timestamp are stored for compliance
- ✅ **Non-Blocking**: Conversion failures don't prevent payment completion
- ✅ **Configurable**: Settlement currency can be configured per payment or globally

### Configuration

Configure the default settlement currency in `appsettings.json`:

```json
{
  "Settlement": {
    "Currency": "USD",
    "Enabled": true
  }
}
```

### How It Works

1. When a payment is completed (via `CompletePaymentCommand` or callback), the settlement service is invoked
2. If the payment currency differs from the settlement currency, conversion is performed
3. The exchange rate is fetched from `IExchangeRateService` at the time of payment completion
4. The converted amount, exchange rate, and settlement currency are stored with the payment
5. If conversion fails, the payment still completes successfully (non-blocking)

### API Response

When a payment has been settled in a different currency, the response includes settlement information:

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

### Complete Payment with Settlement Currency

You can specify a settlement currency when completing a payment:

```http
POST /api/v1/payments/{id}/complete
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "settlementCurrency": "USD"
}
```

If not specified, the default settlement currency from configuration is used.

## See Also

- [Webhook Retry Mechanism](Webhook_Retry_Mechanism.md)
- [Reporting Module](Reporting_Module.md)
- [Security Policy](Security_Policy.md)
- [Tap-to-Pay Integration](TapToPay_Integration.md)
- [System Architecture](../01-Architecture/System_Architecture.md)
- [Extension Guide](../04-Guidelines/Extension_Guide.md)

