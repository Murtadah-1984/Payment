---
title: Webhook Retry Mechanism
version: 1.0
last_updated: 2025-01-15
category: Payment
tags:
  - webhooks
  - retry
  - exponential-backoff
  - reliability
  - notifications
summary: >
  Comprehensive documentation for the webhook retry mechanism with exponential backoff,
  ensuring reliable delivery of payment status change notifications to external systems.
related_docs:
  - Payment_Microservice.md
  - Security_Policy.md
  - ../01-Architecture/System_Architecture.md
ai_context_priority: high
---

# 🔄 Webhook Retry Mechanism

The Payment Microservice includes a robust webhook retry mechanism with exponential backoff to ensure reliable delivery of payment status change notifications to external systems.

## Overview

When payment status changes occur (e.g., payment completed, failed, refunded), the system automatically sends webhooks to configured external systems. If a webhook delivery fails, the system automatically retries with exponential backoff until successful or maximum retries are exhausted.

## Features

- ✅ **Exponential Backoff**: Automatic retry delays increase exponentially (1s, 2s, 4s, 8s, etc.)
- ✅ **Configurable Retries**: Maximum retry attempts configurable per webhook (default: 5)
- ✅ **Background Processing**: Failed webhooks are retried by a background service
- ✅ **Multiple URL Sources**: Supports webhook URLs from payment metadata, merchant config, or default URL
- ✅ **Stateless Design**: Suitable for Kubernetes horizontal scaling
- ✅ **Comprehensive Logging**: All webhook attempts are logged for observability

## Architecture

### Components

1. **WebhookDelivery Entity** (`Payment.Domain.Entities.WebhookDelivery`)
   - Tracks webhook delivery attempts, retry count, and status
   - Manages exponential backoff timing

2. **IWebhookDeliveryService** (`Payment.Domain.Interfaces.IWebhookDeliveryService`)
   - Interface for sending webhooks and scheduling retries

3. **WebhookDeliveryService** (`Payment.Infrastructure.Services.WebhookDeliveryService`)
   - Implements webhook delivery with HTTP client
   - Handles immediate delivery attempts

4. **WebhookRetryService** (`Payment.Infrastructure.BackgroundServices.WebhookRetryService`)
   - Background service that processes pending webhook retries
   - Runs every 10 seconds, processes up to 50 webhooks per cycle

5. **PaymentWebhookNotifier** (`Payment.Application.Services.PaymentWebhookNotifier`)
   - Schedules webhooks when payment status changes
   - Resolves webhook URLs from multiple sources

## Configuration

### appsettings.json

```json
{
  "Webhooks": {
    "MaxRetries": 5,
    "DefaultUrl": "https://default.example.com/webhooks/payment",
    "Merchants": {
      "merchant-id-1": {
        "Url": "https://merchant1.example.com/webhooks/payment"
      },
      "merchant-id-2": {
        "Url": "https://merchant2.example.com/webhooks/payment"
      }
    }
  }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Webhooks:MaxRetries` | int | 5 | Maximum number of retry attempts (1-10) |
| `Webhooks:DefaultUrl` | string | "" | Default webhook URL for all payments |
| `Webhooks:Merchants:{merchantId}:Url` | string | "" | Merchant-specific webhook URL |

## Webhook URL Resolution

The system resolves webhook URLs in the following priority order:

1. **Payment Metadata** (Highest Priority)
   - `webhook_url` or `webhookUrl`
   - `callback_url` or `callbackUrl`

2. **Merchant Configuration**
   - `Webhooks:Merchants:{merchantId}:Url`

3. **Default URL** (Lowest Priority)
   - `Webhooks:DefaultUrl`

If no webhook URL is found, the webhook is not scheduled.

## Exponential Backoff

The retry mechanism uses exponential backoff with the following formula:

```
delay = initialDelay × 2^(retryCount - 1)
```

Where:
- `initialDelay` = 1 second (default)
- Maximum delay is capped at **1 hour**

### Retry Schedule Example

| Retry Attempt | Delay | Total Time Since First Attempt |
|---------------|-------|--------------------------------|
| 1 | 1s | 1s |
| 2 | 2s | 3s |
| 3 | 4s | 7s |
| 4 | 8s | 15s |
| 5 | 16s | 31s |
| 6 | 32s | 63s |
| 7 | 64s | 127s (2m 7s) |
| 8 | 128s | 255s (4m 15s) |
| ... | ... | ... |
| Max | 1 hour | ... |

## Webhook Payload

The webhook payload is a JSON object containing payment information:

```json
{
  "eventType": "payment.completed",
  "paymentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "orderId": "order-456",
  "merchantId": "merchant-123",
  "amount": 100.50,
  "currency": "USD",
  "status": "Succeeded",
  "transactionId": "txn_1234567890",
  "failureReason": null,
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-01-15T10:30:05Z",
  "metadata": {
    "projectId": "XYZ123",
    "userId": "user-789"
  }
}
```

## Event Types

The following event types are supported:

- `payment.processing` - Payment is being processed
- `payment.completed` - Payment completed successfully
- `payment.failed` - Payment failed
- `payment.refunded` - Payment was refunded
- `payment.updated` - Payment status updated (generic)

## HTTP Headers

Webhooks are sent with the following headers:

- `Content-Type: application/json`
- `X-Event-Type: {eventType}` - The type of event
- `User-Agent: Payment-Service/1.0`

## Webhook Delivery Status

Webhooks can have the following statuses:

- **Pending**: Waiting for retry or initial delivery
- **Delivered**: Successfully delivered
- **Failed**: All retry attempts exhausted

## Database Schema

The `WebhookDeliveries` table stores webhook delivery attempts:

```sql
CREATE TABLE "WebhookDeliveries" (
    "Id" UUID PRIMARY KEY,
    "PaymentId" UUID NOT NULL,
    "WebhookUrl" VARCHAR(2048) NOT NULL,
    "EventType" VARCHAR(255) NOT NULL,
    "Payload" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "RetryCount" INTEGER NOT NULL DEFAULT 0,
    "MaxRetries" INTEGER NOT NULL DEFAULT 5,
    "CreatedAt" TIMESTAMP NOT NULL,
    "NextRetryAt" TIMESTAMP,
    "LastAttemptedAt" TIMESTAMP,
    "DeliveredAt" TIMESTAMP,
    "LastError" VARCHAR(2000),
    "LastHttpStatusCode" INTEGER,
    "InitialRetryDelay" BIGINT NOT NULL
);

CREATE INDEX "IX_WebhookDeliveries_PendingRetries" 
    ON "WebhookDeliveries" ("Status", "NextRetryAt");
CREATE INDEX "IX_WebhookDeliveries_PaymentId" 
    ON "WebhookDeliveries" ("PaymentId");
CREATE INDEX "IX_WebhookDeliveries_CreatedAt" 
    ON "WebhookDeliveries" ("CreatedAt");
```

## Usage Examples

### Setting Webhook URL in Payment Metadata

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "idempotencyKey": "payment-order-456-20240115-001",
  "amount": 100.00,
  "currency": "USD",
  "paymentMethod": "CreditCard",
  "provider": "Stripe",
  "merchantId": "merchant-123",
  "orderId": "order-456",
  "metadata": {
    "webhook_url": "https://myapp.example.com/webhooks/payment",
    "projectId": "XYZ123"
  }
}
```

### Querying Webhook Delivery Status

```csharp
// Get all webhooks for a payment
var webhooks = await _unitOfWork.WebhookDeliveries
    .GetByPaymentIdAsync(paymentId, cancellationToken);

// Get failed webhooks
var failedWebhooks = await _unitOfWork.WebhookDeliveries
    .GetFailedDeliveriesAsync(cancellationToken);

// Get pending retries
var pendingRetries = await _unitOfWork.WebhookDeliveries
    .GetPendingRetriesAsync(cancellationToken);
```

## Monitoring and Observability

### Logs

The webhook retry mechanism logs the following events:

- Webhook scheduled
- Webhook delivery attempt (success/failure)
- Retry scheduled with next retry time
- Maximum retries exceeded

### Metrics

Consider adding the following metrics:

- `webhook_delivery_attempts_total` - Total webhook delivery attempts
- `webhook_delivery_success_total` - Successful webhook deliveries
- `webhook_delivery_failures_total` - Failed webhook deliveries
- `webhook_retry_count` - Number of retries per webhook
- `webhook_delivery_duration_seconds` - Webhook delivery duration

## Best Practices

1. **Idempotency**: Ensure your webhook endpoint is idempotent. The same webhook may be delivered multiple times.

2. **Timeout**: Configure appropriate timeouts. The service uses a 30-second timeout by default.

3. **Security**: Validate webhook signatures if possible. Consider implementing HMAC signature validation.

4. **Rate Limiting**: Be aware that retries may come in bursts. Implement rate limiting if necessary.

5. **Monitoring**: Monitor failed webhook deliveries and investigate patterns.

6. **Dead Letter Queue**: Consider implementing a dead letter queue for webhooks that exceed maximum retries.

## Troubleshooting

### Webhooks Not Being Sent

1. Check if webhook URL is configured (metadata, merchant config, or default)
2. Verify payment status changes are triggering webhook notifications
3. Check application logs for webhook scheduling errors

### Webhooks Failing to Deliver

1. Verify the webhook URL is accessible
2. Check if the endpoint returns 2xx status codes
3. Review `LastError` and `LastHttpStatusCode` in the database
4. Check network connectivity and firewall rules

### Retries Not Working

1. Verify `WebhookRetryService` is running (check background services)
2. Check database for pending webhooks with `NextRetryAt` in the past
3. Review application logs for retry service errors

## Testing

Comprehensive tests are available in:

- `tests/Payment.Domain.Tests/Entities/WebhookDeliveryTests.cs`
- `tests/Payment.Infrastructure.Tests/Services/WebhookDeliveryServiceTests.cs`
- `tests/Payment.Infrastructure.Tests/BackgroundServices/WebhookRetryServiceTests.cs`
- `tests/Payment.Application.Tests/Services/PaymentWebhookNotifierTests.cs`

Run tests with:

```bash
dotnet test --filter "FullyQualifiedName~Webhook"
```

## Migration

To add the webhook delivery table, run:

```bash
dotnet ef migrations add AddWebhookDeliveryTable \
  --project src/Payment.Infrastructure \
  --startup-project src/Payment.API
```

Then apply the migration:

```bash
dotnet ef database update \
  --project src/Payment.Infrastructure \
  --startup-project src/Payment.API
```

## Related Documentation

- [Payment Microservice API](Payment_Microservice.md)
- [Security Policy](Security_Policy.md)
- [System Architecture](../01-Architecture/System_Architecture.md)

