---
title: Security Policy & Compliance
version: 1.0
last_updated: 2025-11-11
category: Payment
tags:
  - security
  - pci-dss
  - encryption
  - webhooks
  - idempotency
  - validation
summary: >
  Comprehensive security policy covering webhook signature validation, PCI DSS compliance,
  idempotency keys, input validation, rate limiting, and audit logging.
related_docs:
  - Payment_Microservice.md
  - ../01-Architecture/Authentication_Flow.md
ai_context_priority: high
---

# 🔐 Security Policy & Compliance

## Webhook Signature Validation (CRITICAL Security Feature)

All payment provider callbacks/webhooks are now protected by **HMAC-SHA256 signature validation** to prevent unauthorized payment confirmations.

### How It Works

1. **Middleware Validation**: The `WebhookSignatureValidationMiddleware` intercepts all callback requests before they reach the controller.
2. **Signature Verification**: Each provider must include a valid signature in the request headers (`X-Signature` or provider-specific headers).
3. **Timestamp Validation**: Requests with timestamps older than 5 minutes are rejected to prevent replay attacks.
4. **Provider-Specific Secrets**: Each provider has its own webhook secret configured in `appsettings.json`.

### Configuration

Add `WebhookSecret` to each payment provider configuration:

```json
{
  "PaymentProviders": {
    "ZainCash": {
      "WebhookSecret": "your-zaincash-webhook-secret-key-here",
      // ... other settings
    },
    "FIB": {
      "WebhookSecret": "your-fib-webhook-secret-key-here",
      // ... other settings
    },
    "Telr": {
      "WebhookSecret": "your-telr-webhook-secret-key-here",
      // ... other settings
    }
  }
}
```

### Signature Computation

The middleware expects signatures computed using **HMAC-SHA256**:

- **With timestamp**: `HMAC-SHA256(payload + timestamp, webhookSecret)`
- **Without timestamp**: `HMAC-SHA256(payload, webhookSecret)`

### Request Headers

Payment providers should include these headers in callback requests:

- `X-Signature` (or `X-Webhook-Signature`, `Signature`, etc.) - The computed HMAC-SHA256 signature
- `X-Timestamp` (or `X-Webhook-Timestamp`, `Timestamp`, etc.) - Unix timestamp or ISO 8601 format

### Security Benefits

- ✅ **Prevents forged payment confirmations** - Only requests with valid signatures are processed
- ✅ **Replay attack protection** - Timestamps prevent old requests from being reused
- ✅ **Constant-time comparison** - Signature validation uses constant-time comparison to prevent timing attacks
- ✅ **Provider isolation** - Each provider has its own secret, limiting blast radius if one is compromised

### Testing

The middleware includes comprehensive tests covering:
- Valid signature validation
- Invalid signature rejection
- Missing signature handling
- Expired timestamp rejection
- Multiple header name support
- Timing attack prevention

**⚠️ IMPORTANT**: In production, ensure webhook secrets are:
- Stored in secure secret management (Azure Key Vault, AWS Secrets Manager, K8s Secrets)
- Rotated regularly (quarterly recommended)
- Never committed to version control
- Different for each environment (dev, staging, production)

## Idempotency Keys (CRITICAL Security Feature)

All payment creation requests **must** include an **idempotency key** to prevent duplicate payments from retries, network failures, or client-side errors.

### How It Works

1. **Idempotency Key Required**: Every `CreatePayment` request must include a unique `idempotencyKey` (16-128 characters, alphanumeric with hyphens/underscores).
2. **Request Hash Validation**: The system computes a SHA-256 hash of the request data (amount, currency, metadata, etc.) to ensure the same key isn't reused with different data.
3. **Duplicate Detection**: If an idempotency key is found:
   - **Same request data**: Returns the existing payment (idempotent behavior)
   - **Different request data**: Returns `409 Conflict` with `IdempotencyKeyMismatchException`
4. **Automatic Cleanup**: Expired idempotency records (>24 hours) are automatically cleaned up by a background service.

### Idempotency Key Requirements

- **Length**: 16-128 characters
- **Format**: Alphanumeric characters, hyphens (`-`), and underscores (`_`) only
- **Uniqueness**: Each key must be unique per payment request
- **Lifetime**: Keys are valid for 24 hours after creation

### Example Usage

**Request with Idempotency Key:**
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
  "systemFeePercent": 5.0
}
```

**Idempotent Response (Same Key, Same Data):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 100.00,
  "currency": "USD",
  "status": "Completed",
  ...
}
```

**Conflict Response (Same Key, Different Data):**
```http
HTTP/1.1 409 Conflict
Content-Type: application/json

{
  "error": "Idempotency key 'payment-order-456-20240115-001' was previously used with different request data. Each idempotency key must be unique to a specific request.",
  "idempotencyKey": "payment-order-456-20240115-001"
}
```

### Best Practices

1. **Generate Unique Keys**: Use a combination of order ID, timestamp, and random component:
   ```csharp
   var idempotencyKey = $"{orderId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
   ```

2. **Store Keys Client-Side**: Keep the idempotency key for retry scenarios.

3. **Handle 409 Conflicts**: If you receive a 409 Conflict, it means the key was reused with different data. Generate a new key and retry.

4. **Key Format**: Use descriptive keys for easier debugging:
   ```
   payment-{orderId}-{timestamp}-{sequence}
   ```

### Security Benefits

- ✅ **Prevents duplicate payments** from network retries
- ✅ **Request hash validation** prevents key reuse with different data
- ✅ **Automatic cleanup** prevents database bloat
- ✅ **24-hour retention** balances idempotency with storage efficiency

## PCI DSS Compliance & Data Encryption (CRITICAL Security Feature)

The Payment Microservice implements **PCI DSS compliance** requirements to protect sensitive payment card data and ensure secure handling of financial information.

### Card Tokenization (PCI DSS Requirement 3.4)

**NEVER store the following sensitive card data:**
- ❌ Full credit card number (PAN - Primary Account Number)
- ❌ CVV/CVC (Card Verification Value)
- ❌ Expiration date
- ❌ PIN

**Instead, use tokenization:**
- ✅ **CardToken Value Object**: Stores only tokenized card information from payment providers
- ✅ **Last 4 Digits**: Only the last 4 digits are stored for display purposes
- ✅ **Card Brand**: Visa, Mastercard, Amex, etc. (for display only)
- ✅ **Provider Token**: Payment provider's token (e.g., Stripe token, ZainCash token)

### CardToken Implementation

The `CardToken` value object ensures PCI DSS compliance:

```csharp
var cardToken = new CardToken(
    token: "tok_1234567890",        // Provider's token (never the actual card number)
    last4Digits: "1234",             // Last 4 digits for display
    cardBrand: "Visa"                // Card brand for display
);

// Display masked card
var masked = cardToken.ToMaskedString(); // "**** **** **** 1234"
```

### Metadata Encryption at Rest (PCI DSS Requirement 3.4.1)

All sensitive payment metadata is **encrypted at rest** using **AES-256 encryption**:

- ✅ **Automatic Encryption**: Metadata is automatically encrypted when saved to the database
- ✅ **Automatic Decryption**: Metadata is automatically decrypted when retrieved from the database
- ✅ **AES-256**: Industry-standard encryption algorithm (256-bit key)
- ✅ **Unique IV per Encryption**: Each encryption uses a unique initialization vector (IV) for security

### Configuration

Add the encryption key to `appsettings.json`:

```json
{
  "DataEncryption": {
    "Key": "your-base64-encoded-32-byte-key-here"
  }
}
```

**Generate a secure encryption key:**
```csharp
using System.Security.Cryptography;

var keyBytes = new byte[32]; // 32 bytes = 256 bits for AES-256
RandomNumberGenerator.Fill(keyBytes);
var keyBase64 = Convert.ToBase64String(keyBytes);
Console.WriteLine(keyBase64); // Use this in appsettings.json
```

**⚠️ CRITICAL**: In production:
- Store encryption key in **Azure Key Vault**, **AWS Secrets Manager**, or **Kubernetes Secrets**
- **Rotate encryption keys quarterly** (requires data re-encryption)
- **Never commit keys to version control**
- Use **different keys for each environment** (dev, staging, production)

### Database Schema

The `Payment` entity includes optional `CardToken` information:

```csharp
public class Payment : Entity
{
    // ... other properties ...
    public CardToken? CardToken { get; private set; } // Optional - only for card payments
    
    public void SetCardToken(CardToken cardToken)
    {
        CardToken = cardToken;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

### PaymentDto Response

The API response includes tokenized card information (never full card numbers):

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 100.00,
  "currency": "USD",
  "paymentMethod": "CreditCard",
  "provider": "Stripe",
  "cardToken": {
    "last4Digits": "1234",
    "cardBrand": "Visa"
  },
  // ... other fields ...
}
```

### Security Benefits

- ✅ **PCI DSS Compliance**: Meets PCI DSS Requirement 3.4 (protect stored cardholder data)
- ✅ **Tokenization**: Never stores full card numbers, CVV, or expiration dates
- ✅ **Encryption at Rest**: All sensitive metadata encrypted with AES-256
- ✅ **Field-Level Encryption**: Metadata is encrypted before database storage
- ✅ **Automatic Protection**: Encryption/decryption happens transparently via EF Core value converters
- ✅ **Key Rotation Support**: Encryption service supports key rotation (with data re-encryption)

### Best Practices

1. **Use Payment Provider Tokenization**: Always use the payment provider's tokenization API (Stripe, ZainCash, etc.)
2. **Never Log Card Data**: Ensure logging middleware masks card numbers in logs
3. **Encrypt Sensitive Metadata**: Any sensitive data in metadata is automatically encrypted
4. **Rotate Keys Regularly**: Plan for quarterly encryption key rotation
5. **Monitor Access**: Audit who accesses encrypted data
6. **Secure Key Storage**: Use managed key services (Azure Key Vault, AWS KMS)

### Testing

Comprehensive tests ensure PCI DSS compliance:
- ✅ CardToken validation (prevents storing invalid data)
- ✅ Encryption/decryption round-trip tests
- ✅ Invalid key handling
- ✅ Large data encryption tests
- ✅ Payment entity with CardToken tests

## Input Validation & Sanitization (CRITICAL Security Feature)

The Payment Microservice implements **comprehensive input validation and sanitization** to prevent XSS attacks, SQL injection, data overflow, and other security vulnerabilities.

### Enhanced Input Validation

All payment creation requests are validated with strict rules:

**Amount Validation:**
- ✅ Minimum: Greater than 0
- ✅ Maximum: 1,000,000 (prevents overflow attacks)
- ✅ Decimal precision validation

**Currency Validation:**
- ✅ Must be valid ISO 4217 currency code (USD, EUR, GBP, etc.)
- ✅ Exactly 3 characters
- ✅ Whitelist of supported currencies

**Merchant ID Validation:**
- ✅ Maximum length: 100 characters
- ✅ Allowed characters: Alphanumeric, hyphens, underscores, dots
- ✅ Prevents special characters that could be used in injection attacks

**Order ID Validation:**
- ✅ Maximum length: 100 characters
- ✅ Allowed characters: Alphanumeric, hyphens, underscores only
- ✅ Strict format validation

**Project Code Validation:**
- ✅ Maximum length: 100 characters
- ✅ Allowed characters: Alphanumeric, hyphens, underscores only

**Metadata Validation:**
- ✅ Maximum keys: 50
- ✅ Maximum value length: 1KB (1000 characters) per value
- ✅ Key format: Alphanumeric, hyphens, underscores only
- ✅ XSS protection: Blocks dangerous patterns:
  - `<script>` tags
  - `javascript:` protocol
  - Event handlers (`onerror=`, `onclick=`, `onload=`, etc.)
  - `vbscript:` protocol
  - `data:text/html` URLs
  - HTML entity encoding (`&#x`, `&#60;`)
  - `eval()` and `expression()` functions

**Callback URL Validation:**
- ✅ Must be HTTPS (HTTP not allowed)
- ✅ Maximum length: 2048 characters
- ✅ Valid URI format validation

**Customer Email Validation:**
- ✅ RFC 5321 compliant email format
- ✅ Maximum length: 254 characters
- ✅ Domain validation (must contain dot, cannot start/end with dot)
- ✅ Local part validation

**Customer Phone Validation:**
- ✅ Maximum length: 20 characters
- ✅ International format support
- ✅ Minimum 7 digits required
- ✅ Allowed characters: Digits, spaces, hyphens, parentheses, dots, plus sign

### Security Headers Middleware

The `RequestSanitizationMiddleware` adds critical security headers to all HTTP responses:

**X-Content-Type-Options: `nosniff`**
- Prevents browsers from MIME-type sniffing
- Protects against content-type confusion attacks

**X-Frame-Options: `DENY`**
- Prevents clickjacking attacks
- Blocks page from being displayed in frames/iframes

**X-XSS-Protection: `1; mode=block`**
- Enables XSS filtering in older browsers
- Modern browsers have built-in protection, but this helps with legacy support

**Referrer-Policy: `strict-origin-when-cross-origin`**
- Limits referrer information sent to other sites
- Protects user privacy

**Content-Security-Policy (CSP)**
- Strict CSP policy restricts resource loading:
  - `default-src 'self'` - Only allow resources from same origin
  - `script-src 'self'` - Only allow scripts from same origin
  - `style-src 'self' 'unsafe-inline'` - Allow inline styles (for Swagger UI)
  - `img-src 'self' data: https:` - Allow images from same origin, data URIs, and HTTPS
  - `frame-ancestors 'none'` - Prevent framing
  - `upgrade-insecure-requests` - Automatically upgrade HTTP to HTTPS

**Permissions-Policy**
- Restricts access to browser features:
  - `geolocation=()` - Disable geolocation
  - `microphone=()` - Disable microphone
  - `camera=()` - Disable camera
  - `payment=()` - Disable payment API
  - And other sensitive APIs

**Strict-Transport-Security (HSTS)**
- Only added for HTTPS requests
- `max-age=31536000` - Force HTTPS for 1 year
- `includeSubDomains` - Apply to all subdomains
- `preload` - Enable HSTS preload

### Implementation Details

**Validator Location: `src/Payment.Application/Validators/CreatePaymentCommandValidator.cs`**

**Middleware Location: `src/Payment.API/Middleware/RequestSanitizationMiddleware.cs`**

### Security Benefits

- ✅ **XSS Protection**: Blocks script injection in metadata and input fields
- ✅ **SQL Injection Prevention**: Parameterized queries (EF Core) + input validation
- ✅ **Data Overflow Prevention**: Maximum length and amount limits
- ✅ **Clickjacking Protection**: X-Frame-Options header
- ✅ **MIME-Type Sniffing Protection**: X-Content-Type-Options header
- ✅ **Content Security**: Strict CSP policy
- ✅ **Privacy Protection**: Referrer-Policy and Permissions-Policy
- ✅ **HTTPS Enforcement**: HSTS header for secure connections

### Best Practices

1. **Always Validate Input**: Never trust user input, always validate
2. **Use Whitelist Validation**: Only allow known good values (e.g., currency codes)
3. **Limit Data Size**: Set maximum lengths to prevent DoS attacks
4. **Sanitize Metadata**: Check for dangerous patterns before storing
5. **Enforce HTTPS**: Always use HTTPS in production
6. **Keep Headers Updated**: Review and update security headers regularly
7. **Test Validation Rules**: Comprehensive unit tests ensure validation works correctly

### Testing

Comprehensive tests ensure input validation works correctly:
- ✅ Amount validation tests (zero, negative, overflow)
- ✅ Currency validation tests (invalid codes, length)
- ✅ Metadata validation tests (size limits, XSS patterns)
- ✅ Security headers middleware tests
- ✅ Phone number format validation tests
- ✅ Email validation tests
- ✅ URL validation tests

## Rate Limiting & DDoS Protection (HIGH Priority Security Feature)

The Payment Microservice implements **IP-based rate limiting** to prevent DDoS attacks and API abuse.

### Configuration

Rate limiting is configured in `appsettings.json`:

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Real-IP",
    "ClientIdHeader": "X-ClientId",
    "HttpStatusCode": 429,
    "GeneralRules": [
      {
        "Endpoint": "POST:/api/v1/payments",
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

### Features

- ✅ **Endpoint-specific limits**: Different limits for different endpoints (e.g., 10 requests/minute for payment creation)
- ✅ **IP-based tracking**: Tracks requests by IP address or client ID
- ✅ **429 Too Many Requests**: Returns HTTP 429 when limit is exceeded
- ✅ **Configurable rules**: Easy to adjust limits per endpoint
- ✅ **Memory-based storage**: Uses in-memory cache for rate limit tracking (can be upgraded to Redis for distributed scenarios)

### Production Recommendations

- Use **Redis-backed rate limiting** for distributed deployments
- Configure limits based on expected traffic patterns
- Monitor rate limit hits to detect abuse patterns
- Consider implementing per-user rate limits in addition to IP-based limits

## Audit Logging (HIGH Priority Security Feature)

The Payment Microservice implements **comprehensive audit logging** for compliance and security tracking.

### Features

- ✅ **Automatic logging**: All mutating operations (commands) are automatically logged
- ✅ **User tracking**: Captures user ID from JWT token
- ✅ **IP address tracking**: Records client IP address
- ✅ **Change tracking**: Stores request data (with sensitive fields redacted)
- ✅ **Timestamp tracking**: UTC timestamps for all audit entries
- ✅ **Entity tracking**: Links audit logs to specific entities (payments, etc.)

### Audit Log Structure

```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; }           // User from JWT token
    public string Action { get; set; }          // e.g., "CreatePaymentCommand"
    public string EntityType { get; set; }      // e.g., "Payment"
    public Guid EntityId { get; set; }          // Payment ID
    public string? IpAddress { get; set; }      // Client IP
    public string? UserAgent { get; set; }      // Browser/client info
    public Dictionary<string, object> Changes { get; set; }  // Request data
    public DateTime Timestamp { get; set; }     // UTC timestamp
}
```

### Querying Audit Logs

The `IAuditLogRepository` provides methods to query audit logs:

```csharp
// Get all audit logs for a user
var userLogs = await _auditLogRepository.GetByUserIdAsync("user-123");

// Get audit logs for a specific payment
var paymentLogs = await _auditLogRepository.GetByEntityAsync("Payment", paymentId);

// Get audit logs by action
var createLogs = await _auditLogRepository.GetByActionAsync("CreatePaymentCommand");

// Get audit logs by date range
var recentLogs = await _auditLogRepository.GetByDateRangeAsync(
    DateTime.UtcNow.AddDays(-30), 
    DateTime.UtcNow);
```

### Security & Compliance

- ✅ **Sensitive data redaction**: Secrets, passwords, and tokens are automatically redacted
- ✅ **Immutable logs**: Audit logs are append-only (no updates/deletes)
- ✅ **7-year retention**: Recommended for financial data compliance
- ✅ **Indexed queries**: Fast lookups by user, entity, action, or date range

## See Also

- [Payment Microservice](Payment_Microservice.md)
- [Authentication Flow](../01-Architecture/Authentication_Flow.md)
- [Kubernetes Deployment](../03-Infrastructure/Kubernetes_Deployment.md)

