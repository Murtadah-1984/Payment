---
title: Automated Monthly Reporting System
version: 1.0
last_updated: 2025-11-11
category: Payment
tags:
  - reporting
  - monthly-reports
  - cronjob
  - prometheus
  - metrics
summary: >
  Automated monthly financial reporting system that generates comprehensive reports,
  publishes events, and exposes Prometheus metrics for observability.
related_docs:
  - Payment_Microservice.md
  - ../03-Infrastructure/Observability.md
ai_context_priority: medium
---

# 🧾 Automated Monthly Reporting System

The Payment Microservice includes an **automated monthly financial reporting system** that generates comprehensive reports at the end of each month, publishes events to the Notification Microservice, and exposes metrics for observability.

## Features

- ✅ **Automatic Report Generation** - Runs at midnight UTC on the 1st day of each month via Kubernetes CronJob
- ✅ **Comprehensive Aggregations** - Aggregates payments, refunds, system fees, merchant splits, and partner payouts
- ✅ **Multi-Format Reports** - Generates both PDF and CSV files per project
- ✅ **Secure Storage** - Uploads reports to S3/MinIO/Azure Blob Storage
- ✅ **Event-Driven Notifications** - Publishes `MonthlyReportGeneratedEvent` to message bus (RabbitMQ/Kafka/Azure Service Bus)
- ✅ **Manual Trigger** - Admin API endpoint to manually regenerate reports
- ✅ **Idempotency** - Prevents duplicate report generation
- ✅ **Prometheus Metrics** - Exposes metrics for Grafana dashboards
- ✅ **Error Resilience** - Retry publishing events with exponential backoff

## Schedule

Reports are automatically generated at **midnight UTC on the 1st day of each month** via Kubernetes CronJob. The CronJob runs the payment service with the `--generate-report` flag, which generates a report for the previous month.

**Cron Schedule:** `0 0 1 * *` (runs at 00:00 UTC on the 1st of every month)

## Report Contents

Each monthly report includes:

- **Total Processed** - Sum of all successful payments
- **Total Refunded** - Sum of all refunds
- **Total System Fees** - System fees collected
- **Total Merchant Payouts** - Amount paid to merchants
- **Total Partner Payouts** - Amount paid to partners (from payment splits)
- **Breakdown by Project** - Aggregated totals per project code
- **Breakdown by Provider** - Aggregated totals per payment provider
- **Breakdown by Currency** - Aggregated totals per currency
- **Transaction Counts** - Total, successful, and failed transaction counts
- **Refund Count** - Total number of refunds

## API Endpoints

### Manual Report Generation

**Endpoint:** `POST /api/v1/admin/reports/monthly/generate`

**Query Parameters:**
- `year` (required) - Year (2000-2100)
- `month` (required) - Month (1-12)

**Authentication:** Requires `SystemOwner` role

**Example Request:**
```http
POST /api/v1/admin/reports/monthly/generate?year=2025&month=10
Authorization: Bearer {your-jwt-token}
```

**Example Response:**
```json
{
  "message": "Report generation triggered successfully.",
  "reportId": "fa5b0b34-abc1-4ad9-b72d-3c0ad5ddac0a",
  "reportUrl": "https://storage.company.com/reports/2025-10.pdf",
  "pdfUrl": "https://storage.company.com/reports/2025-10.pdf",
  "csvUrl": "https://storage.company.com/reports/2025-10.csv",
  "year": 2025,
  "month": 10
}
```

## Event Publishing

When a report is generated, the system publishes a `MonthlyReportGeneratedEvent` to the message bus:

**Topic:** `payment.reports.monthly.generated`

**Event Payload:**
```json
{
  "reportId": "fa5b0b34-abc1-4ad9-b72d-3c0ad5ddac0a",
  "year": 2025,
  "month": 10,
  "projectCode": "ALL",
  "reportUrl": "https://storage.company.com/reports/2025-10.pdf",
  "generatedAtUtc": "2025-11-01T00:10:00Z",
  "occurredOn": "2025-11-01T00:10:00Z"
}
```

The **Notification Microservice** should subscribe to this topic and send notifications (email, Slack, dashboard) to system owners.

## Prometheus Metrics

The following metrics are exposed on the `/metrics` endpoint:

- `payment_reports_generated_total{project, status}` - Total number of reports generated (success/failure)
- `payment_reports_failures_total{project, error_type}` - Total number of report generation failures
- `payment_reports_last_duration_seconds{project}` - Duration of report generation in seconds (histogram)
- `payment_reports_last_generation_timestamp{project}` - Unix timestamp of last successful report generation

**Example Grafana Query:**
```promql
# Report generation success rate
rate(payment_reports_generated_total{status="success"}[5m]) / 
rate(payment_reports_generated_total[5m])

# Average report generation duration
histogram_quantile(0.95, payment_reports_last_duration_seconds)
```

## Kubernetes Deployment

### CronJob Configuration

The CronJob is defined in `k8s/payment-report-cronjob.yaml`:

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: payment-report-generator
  namespace: payment
spec:
  schedule: "0 0 1 * *"  # Runs at midnight UTC on 1st of each month
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: payment-report
              image: yourregistry/payment-service:latest
              command: ["dotnet", "Payment.API.dll", "--generate-report"]
```

**Deploy the CronJob:**
```bash
kubectl apply -f k8s/payment-report-cronjob.yaml
```

**Check CronJob Status:**
```bash
kubectl get cronjobs -n payment
kubectl get jobs -n payment
kubectl logs -n payment job/payment-report-generator-<timestamp>
```

## Configuration

Configure storage and messaging in `appsettings.json`:

```json
{
  "Storage": {
    "BaseUrl": "https://storage.company.com",
    "BucketName": "payment-reports"
  },
  "Messaging": {
    "Broker": "RabbitMQ",  // or "Kafka", "AzureServiceBus"
    "ConnectionString": "..."
  }
}
```

## Error Handling

- **Idempotency**: Reports are not regenerated if they already exist for the given month/project
- **Retry Policy**: Event publishing uses exponential backoff (max 3 retries)
- **Logging**: All failures are logged with structured logging (Serilog)
- **Metrics**: Failures are tracked in Prometheus metrics

## Testing

**Run Report Generation Locally:**
```bash
dotnet run --project src/Payment.API -- --generate-report
```

**Unit Tests:**
```bash
dotnet test tests/Payment.Application.Tests/Services/PaymentReportingSchedulerTests.cs
```

## See Also

- [Payment Microservice](Payment_Microservice.md)
- [Observability & Monitoring](../03-Infrastructure/Observability.md)
- [Kubernetes Deployment](../03-Infrastructure/Kubernetes_Deployment.md)

