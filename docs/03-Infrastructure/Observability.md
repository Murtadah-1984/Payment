---
title: Observability & Distributed Tracing
version: 1.0
last_updated: 2025-11-11
category: Infrastructure
tags:
  - observability
  - opentelemetry
  - jaeger
  - zipkin
  - prometheus
  - health-checks
  - tracing
summary: >
  Comprehensive observability documentation covering OpenTelemetry integration,
  distributed tracing with Jaeger/Zipkin, health checks, and monitoring.
related_docs:
  - Kubernetes_Deployment.md
  - Performance_Optimization.md
  - ../02-Payment/Reporting_Module.md
ai_context_priority: medium
---

# 🔍 Observability & Distributed Tracing

The Payment Microservice implements **comprehensive observability** using **OpenTelemetry** for distributed tracing, metrics, and correlation between logs and traces.

## Features

- ✅ **OpenTelemetry Integration**: Full OpenTelemetry support with ASP.NET Core, HTTP Client, and EF Core instrumentation
- ✅ **Jaeger Exporter**: Export traces to Jaeger for visualization and analysis
- ✅ **Zipkin Exporter**: Export traces to Zipkin (alternative to Jaeger)
- ✅ **Prometheus Metrics**: Export metrics to Prometheus for monitoring
- ✅ **Correlation IDs**: Automatic trace and span IDs in all log entries
- ✅ **Custom Spans**: Custom ActivitySource spans for critical operations (CreatePayment, GetPaymentById, FailPayment, RefundPayment)
- ✅ **EF Core Instrumentation**: Automatic tracing of database queries
- ✅ **HTTP Client Instrumentation**: Automatic tracing of external API calls

## Configuration

Configure OpenTelemetry in `appsettings.json`:

```json
{
  "OpenTelemetry": {
    "UseConsoleExporter": true,
    "Jaeger": {
      "Host": "localhost",
      "Port": 6831
    },
    "Zipkin": {
      "Endpoint": "http://localhost:9411/api/v2/spans"
    }
  }
}
```

## Custom Spans in Handlers

All critical handlers include custom OpenTelemetry spans with relevant tags:

```csharp
// Example from GetPaymentByIdQueryHandler
using var activity = ActivitySource.StartActivity("GetPaymentById");
activity?.SetTag("payment.id", request.PaymentId.ToString());
activity?.SetTag("cache.hit", true);
activity?.SetTag("payment.status", dto.Status);
activity?.SetStatus(ActivityStatusCode.Ok);
```

## Correlation IDs in Logs

All log entries automatically include trace and span IDs for correlation:

```
[2024-01-15 10:30:00.123] [INF] [TraceId: abc123...] [SpanId: def456...] Creating payment for order order-456
```

## Benefits

- ✅ **End-to-End Tracing**: Track requests across all services and components
- ✅ **Performance Analysis**: Identify bottlenecks in payment processing
- ✅ **Error Correlation**: Link errors in logs to specific traces
- ✅ **Distributed Debugging**: Debug issues across microservices
- ✅ **Metrics Integration**: Export metrics to Prometheus for dashboards

## Enhanced Health Checks

The Payment Microservice implements **comprehensive health checks** for Kubernetes deployment, including separate liveness and readiness probes.

### Health Check Endpoints

- **`/health/live`**: Liveness probe - checks if the service is alive (lightweight checks)
- **`/health/ready`**: Readiness probe - checks if the service is ready to accept traffic (database, cache, etc.)
- **`/health`**: General health check (all checks)
- **`/ready`**: Legacy readiness endpoint (backward compatibility)

### Implemented Health Checks

1. **PostgreSQL Database** (`postgresql`)
   - Tags: `db`, `ready`
   - Checks database connectivity and EF Core context

2. **Redis Cache** (`redis`)
   - Tags: `cache`, `ready`
   - Checks Redis connectivity (if configured)

3. **Payment Providers** (`payment-providers`)
   - Tags: `provider`, `live`
   - Verifies all payment providers are registered and operational

4. **Disk Space** (`disk-space`)
   - Tags: `infrastructure`, `live`
   - Checks available disk space (default: 10% minimum required)

### Configuration

```csharp
// src/Payment.API/Program.cs
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: connectionString,
        name: "postgresql",
        tags: new[] { "db", "ready" })
    .AddDbContextCheck<PaymentDbContext>(tags: new[] { "db", "ready" })
    .AddRedis(
        redisConnectionString: redisConnectionString,
        name: "redis",
        tags: new[] { "cache", "ready" })
    .AddCheck<PaymentProviderHealthCheck>(
        "payment-providers",
        tags: new[] { "provider", "live" })
    .AddCheck<DiskSpaceHealthCheck>(
        "disk-space",
        tags: new[] { "infrastructure", "live" });

// Separate endpoints for Kubernetes probes
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### Kubernetes Configuration

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: payment-api
spec:
  template:
    spec:
      containers:
      - name: payment-api
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
```

### Health Check Response Format

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "postgresql": {
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "data": {}
    },
    "payment-providers": {
      "status": "Healthy",
      "duration": "00:00:00.0012345",
      "data": {
        "ProviderCount": 12,
        "Providers": "ZainCash, Stripe, FIB, ..."
      }
    },
    "disk-space": {
      "status": "Healthy",
      "duration": "00:00:00.0001234",
      "data": {
        "Drive": "C:\\",
        "FreeSpacePercent": 45.67,
        "MinimumFreeSpacePercent": 10.0
      }
    }
  }
}
```

### Benefits

- ✅ **Kubernetes-Ready**: Separate liveness and readiness probes
- ✅ **Comprehensive Monitoring**: Checks database, cache, providers, and infrastructure
- ✅ **Detailed Information**: Health checks return detailed diagnostic data
- ✅ **Fast Failures**: Unhealthy services are quickly detected and restarted
- ✅ **Production-Ready**: UI response writer provides human-readable health status

## Production Configuration

For production deployment, configure OpenTelemetry exporters with Jaeger or Zipkin:

### Option 1: Jaeger Configuration (Recommended)

**Kubernetes Deployment:**
```bash
# Deploy Jaeger all-in-one (includes agent, collector, and UI)
kubectl apply -f k8s/jaeger-deployment.yaml

# Verify deployment
kubectl get pods -n payment -l app=jaeger
kubectl get svc -n payment | grep jaeger

# Port-forward to access Jaeger UI locally
kubectl port-forward -n payment svc/jaeger-query 16686:16686
```

**Service Endpoints:**
- **Jaeger Agent (UDP)**: `jaeger-agent:6831` - Receives traces from applications
- **Jaeger Collector (HTTP)**: `jaeger-collector:14268` - Alternative HTTP endpoint
- **Jaeger Collector (gRPC)**: `jaeger-collector:14250` - gRPC endpoint
- **Jaeger UI**: `jaeger-query:16686` - Web interface for trace visualization

**ConfigMap Configuration:**
```yaml
# k8s/configmap.yaml
data:
  OpenTelemetry__UseConsoleExporter: "false"
  OpenTelemetry__Jaeger__Host: "jaeger-agent"
  OpenTelemetry__Jaeger__Port: "6831"
```

**Accessing Jaeger UI:**
- **Local (Port-forward)**: `http://localhost:16686`
- **Kubernetes (LoadBalancer)**: `http://<jaeger-query-service-ip>:16686`
- **Ingress**: Configure ingress rule for `jaeger-query` service

### Option 2: Zipkin Configuration (Alternative)

**Kubernetes Deployment:**
```bash
# Deploy Zipkin
kubectl apply -f k8s/zipkin-deployment.yaml

# Verify deployment
kubectl get pods -n payment -l app=zipkin
kubectl get svc -n payment | grep zipkin

# Port-forward to access Zipkin UI locally
kubectl port-forward -n payment svc/zipkin 9411:9411
```

**Service Endpoints:**
- **Zipkin API**: `zipkin:9411/api/v2/spans` - Receives traces via HTTP
- **Zipkin UI**: `zipkin:9411` - Web interface for trace visualization

**ConfigMap Configuration:**
```yaml
# k8s/configmap.yaml
data:
  OpenTelemetry__UseConsoleExporter: "false"
  OpenTelemetry__Zipkin__Endpoint: "http://zipkin:9411/api/v2/spans"
```

**Accessing Zipkin UI:**
- **Local (Port-forward)**: `http://localhost:9411`
- **Kubernetes (LoadBalancer)**: `http://<zipkin-service-ip>:9411`
- **Ingress**: Configure ingress rule for `zipkin` service

### Docker Compose (Local Development)

For local development, use Docker Compose to run all services:

```bash
# Start all services including Jaeger and Zipkin
docker-compose up -d

# Verify services are running
docker-compose ps

# View logs
docker-compose logs -f payment-api
docker-compose logs -f jaeger
docker-compose logs -f zipkin
```

**Service URLs:**
- **Payment API**: `http://localhost:5000`
- **Jaeger UI**: `http://localhost:16686`
- **Zipkin UI**: `http://localhost:9411`

### Production Verification

**1. Verify Traces are Being Sent:**
```bash
# Check application logs for OpenTelemetry activity
kubectl logs -n payment deployment/payment-api | grep -i "opentelemetry\|trace\|span"

# Check Jaeger collector logs
kubectl logs -n payment deployment/jaeger | grep -i "trace\|span"
```

**2. Verify Traces in Jaeger UI:**
1. Open Jaeger UI: `http://localhost:16686` (or production URL)
2. Select service: `Payment.API`
3. Click "Find Traces"
4. Verify traces appear for payment operations (CreatePayment, GetPaymentById, etc.)

**3. Verify Traces in Zipkin UI:**
1. Open Zipkin UI: `http://localhost:9411` (or production URL)
2. Click "Run Query"
3. Select service: `Payment.API`
4. Verify traces appear for payment operations

**4. Verify Custom Spans:**
- Check for custom spans: `GetPaymentById`, `CreatePayment`, `FailPayment`, `RefundPayment`
- Verify tags are present: `payment.id`, `payment.status`, `cache.hit`, `transaction.id`
- Verify status codes: `Ok` for success, `Error` for failures

### Production Best Practices

- ✅ **Use Jaeger for Production**: More feature-rich, better for complex microservices
- ✅ **Disable Console Exporter**: Set `OpenTelemetry__UseConsoleExporter: "false"` in production
- ✅ **Configure Resource Limits**: Set appropriate CPU/memory limits for Jaeger/Zipkin pods
- ✅ **Use Persistent Storage**: Configure persistent volumes for Jaeger storage (not in-memory)
- ✅ **Enable Sampling**: Configure sampling rates to reduce trace volume in high-traffic scenarios
- ✅ **Monitor Trace Volume**: Set up alerts for trace ingestion rates
- ✅ **Secure Access**: Use ingress with authentication for Jaeger/Zipkin UI in production

See `k8s/README-OpenTelemetry.md` for detailed production setup instructions.

## Prometheus Metrics

The Payment Microservice exposes **comprehensive Prometheus metrics** for monitoring and alerting, covering business, technical, and security metrics as specified in the remediation instructions.

### Business Metrics

Business metrics track key performance indicators for payment operations:

| Metric | Type | Labels | Description | Target |
|--------|------|--------|-------------|--------|
| `payment_total` | Counter | `provider`, `status` | Total number of payment attempts | - |
| `payment_processing_duration_seconds` | Histogram | `provider`, `status` | Payment processing duration | <2s |
| `payment_provider_availability` | Gauge | `provider` | Provider availability (1=available, 0=unavailable) | >99.9% |
| `payment_refund_total` | Counter | `provider`, `status` | Total number of refunds processed | - |
| `payment_refund_processing_duration_seconds` | Histogram | `provider`, `status` | Refund processing duration | - |
| `payment_fraud_detection_total` | Counter | `result`, `action` | Fraud detection checks (detected/not_detected, blocked/allowed) | - |

**Example PromQL Queries:**

```promql
# Payment success rate (target: >99%)
rate(payment_total{status="succeeded"}[5m]) / 
rate(payment_total[5m])

# Average payment processing time (target: <2s)
histogram_quantile(0.50, payment_processing_duration_seconds)

# Provider availability (target: >99.9%)
avg_over_time(payment_provider_availability[5m])

# Refund rate
rate(payment_refund_total[5m]) / rate(payment_total[5m])

# Fraud detection accuracy
rate(payment_fraud_detection_total{result="detected"}[5m]) / 
rate(payment_fraud_detection_total[5m])
```

### Technical Metrics

Technical metrics track system performance and health:

| Metric | Type | Labels | Description | Target |
|--------|------|--------|-------------|--------|
| `payment_api_response_time_seconds` | Histogram | `endpoint`, `method`, `status_code` | API response time (p50, p95, p99) | - |
| `payment_database_query_time_seconds` | Histogram | `operation`, `table` | Database query execution time | - |
| `payment_cache_operations_total` | Counter | `operation`, `result` | Cache operations (get/set, hit/miss) | >80% hit rate |
| `payment_errors_total` | Counter | `type`, `severity`, `component` | Total number of errors | <0.1% |
| `payment_circuit_breaker_state` | Gauge | `provider` | Circuit breaker state (0=closed, 1=open, 2=half-open) | - |
| `payment_queue_depth` | Gauge | `queue_name` | Current queue depth | - |

**Example PromQL Queries:**

```promql
# API response time percentiles
histogram_quantile(0.50, payment_api_response_time_seconds)  # P50
histogram_quantile(0.95, payment_api_response_time_seconds) # P95
histogram_quantile(0.99, payment_api_response_time_seconds) # P99

# Database query time
histogram_quantile(0.95, payment_database_query_time_seconds)

# Cache hit rate (target: >80%)
rate(payment_cache_operations_total{operation="get", result="hit"}[5m]) / 
rate(payment_cache_operations_total{operation="get"}[5m])

# Error rate (target: <0.1%)
rate(payment_errors_total[5m]) / rate(payment_total[5m])

# Circuit breaker status
payment_circuit_breaker_state

# Queue depth
payment_queue_depth
```

**Note:** CPU and Memory usage metrics are automatically provided by OpenTelemetry runtime instrumentation.

### Security Metrics

Security metrics track authentication, authorization, and security events:

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `payment_authentication_failures_total` | Counter | `reason` | Failed authentication attempts (invalid_token, expired_token, missing_token) |
| `payment_rate_limit_hits_total` | Counter | `endpoint`, `ip_address` | Rate limit hits |
| `payment_webhook_signature_failures_total` | Counter | `provider`, `reason` | Webhook signature validation failures (invalid_signature, missing_signature, expired_timestamp) |
| `payment_suspicious_activity_alerts_total` | Counter | `type`, `severity` | Suspicious activity alerts (fraud, replay_attack, unusual_pattern, high/medium/low) |

**Example PromQL Queries:**

```promql
# Failed authentication attempts
rate(payment_authentication_failures_total[5m])

# Rate limit hits
rate(payment_rate_limit_hits_total[5m])

# Webhook signature failures
rate(payment_webhook_signature_failures_total[5m])

# Suspicious activity alerts
rate(payment_suspicious_activity_alerts_total{severity="high"}[5m])
```

### Tap-to-Pay Metrics

Tap-to-Pay transactions expose dedicated metrics:

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `payment_tap_to_pay_total` | Counter | `status` | Total Tap-to-Pay transactions (succeeded/failed) |
| `payment_tap_to_pay_failures_total` | Counter | `error_type` | Total failures by error type |
| `payment_tap_to_pay_duration_seconds` | Histogram | `status` | Transaction processing duration |
| `payment_tap_to_pay_replay_attempts_total` | Counter | - | Total detected replay attempts |
| `payment_tap_to_pay_last_transaction_timestamp` | Gauge | `status` | Unix timestamp of last transaction |

**Example PromQL Queries:**

```promql
# Tap-to-Pay success rate
rate(payment_tap_to_pay_total{status="succeeded"}[5m]) / 
rate(payment_tap_to_pay_total[5m])

# Tap-to-Pay failure rate by error type
rate(payment_tap_to_pay_failures_total[5m])

# Tap-to-Pay P95 latency
histogram_quantile(0.95, payment_tap_to_pay_duration_seconds)

# Replay attempts per minute
rate(payment_tap_to_pay_replay_attempts_total[5m])
```

### Report Generation Metrics

Report generation metrics are documented in [Reporting Module](../02-Payment/Reporting_Module.md).

### Metrics Integration

Metrics are automatically recorded throughout the payment processing flow:

- **Payment Processing**: `PaymentOrchestrator` records payment attempts, processing time, and provider availability
- **Refunds**: `RefundPaymentCommandHandler` records refund operations
- **Cache Operations**: `RedisCacheService` and `MemoryCacheService` track cache hits/misses
- **Circuit Breaker**: `ResilientPaymentProviderDecorator` tracks circuit breaker state changes
- **Webhook Validation**: `WebhookSignatureValidationMiddleware` tracks signature failures
- **Fraud Detection**: `CreatePaymentCommandHandler` tracks fraud detection results
- **Authentication**: `AuthenticationMetricsMiddleware` tracks authentication failures
- **Rate Limiting**: `RateLimitMetricsMiddleware` tracks rate limit hits

All metrics are exposed via the `IMetricsRecorder` interface, following Clean Architecture principles.

### Metrics Endpoint

Metrics are exposed on the `/metrics` endpoint:

```bash
# Access metrics
curl http://localhost:5000/metrics

# In Kubernetes
curl http://payment-api/metrics
```

**Note:** In production, the `/metrics` endpoint should be behind internal network or protected by authentication.

### Testing Metrics

Comprehensive tests are available for all metrics:

- **Unit Tests**: `PaymentMetricsTests` - Tests all metric recording methods
- **Integration Tests**: `MetricsRecorderTests` - Tests the metrics recorder implementation
- **Middleware Tests**: `AuthenticationMetricsMiddlewareTests`, `RateLimitMetricsMiddlewareTests` - Tests middleware metrics tracking

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~Metrics"
```

### Grafana Dashboards

Recommended Grafana dashboards:

1. **Payment Overview Dashboard**
   - Transaction volume by provider
   - Payment success rate (target: >99%)
   - Average payment processing time (target: <2s)
   - Latency percentiles (P50, P95, P99)
   - Error breakdown
   - Refund rate

2. **Technical Performance Dashboard**
   - API response time percentiles
   - Database query time
   - Cache hit rate (target: >80%)
   - Error rate (target: <0.1%)
   - Circuit breaker status by provider
   - Queue depth

3. **Security Dashboard**
   - Failed authentication attempts
   - Rate limit hits
   - Webhook signature failures
   - Suspicious activity alerts by severity
   - Fraud detection accuracy

4. **Tap-to-Pay Dashboard**
   - Tap-to-Pay transaction volume
   - Success rate trends
   - Replay attempt alerts
   - Error type distribution
   - Latency analysis

5. **Provider Health Dashboard**
   - Provider availability (target: >99.9%)
   - Provider-specific metrics
   - Health check status
   - Circuit breaker state per provider

### Alerting Rules

Example Prometheus alerting rules covering all metrics:

```yaml
groups:
  - name: payment_business_alerts
    rules:
      # Payment success rate (target: >99%)
      - alert: LowPaymentSuccessRate
        expr: (rate(payment_total{status="succeeded"}[5m]) / rate(payment_total[5m])) < 0.99
        for: 5m
        annotations:
          summary: "Payment success rate below 99%"
          description: "Payment success rate is {{ $value | humanizePercentage }}"
          
      # Average payment processing time (target: <2s)
      - alert: HighPaymentProcessingTime
        expr: histogram_quantile(0.50, payment_processing_duration_seconds) > 2
        for: 5m
        annotations:
          summary: "Average payment processing time exceeds 2 seconds"
          description: "Average processing time is {{ $value }}s"
          
      # Provider availability (target: >99.9%)
      - alert: LowProviderAvailability
        expr: avg_over_time(payment_provider_availability[5m]) < 0.999
        for: 5m
        annotations:
          summary: "Provider availability below 99.9%"
          description: "Provider {{ $labels.provider }} availability is {{ $value | humanizePercentage }}"
          
  - name: payment_technical_alerts
    rules:
      # Cache hit rate (target: >80%)
      - alert: LowCacheHitRate
        expr: (rate(payment_cache_operations_total{operation="get", result="hit"}[5m]) / rate(payment_cache_operations_total{operation="get"}[5m])) < 0.80
        for: 5m
        annotations:
          summary: "Cache hit rate below 80%"
          description: "Cache hit rate is {{ $value | humanizePercentage }}"
          
      # Error rate (target: <0.1%)
      - alert: HighErrorRate
        expr: (rate(payment_errors_total[5m]) / rate(payment_total[5m])) > 0.001
        for: 5m
        annotations:
          summary: "Error rate exceeds 0.1%"
          description: "Error rate is {{ $value | humanizePercentage }}"
          
      # Circuit breaker open
      - alert: CircuitBreakerOpen
        expr: payment_circuit_breaker_state == 1
        for: 1m
        annotations:
          summary: "Circuit breaker is open for provider {{ $labels.provider }}"
          
  - name: payment_security_alerts
    rules:
      # High authentication failures
      - alert: HighAuthenticationFailures
        expr: rate(payment_authentication_failures_total[5m]) > 10
        for: 5m
        annotations:
          summary: "High rate of authentication failures detected"
          description: "{{ $value }} authentication failures per second"
          
      # Rate limit hits
      - alert: RateLimitHits
        expr: rate(payment_rate_limit_hits_total[5m]) > 5
        for: 5m
        annotations:
          summary: "High rate of rate limit hits"
          description: "{{ $value }} rate limit hits per second on {{ $labels.endpoint }}"
          
      # Webhook signature failures
      - alert: WebhookSignatureFailures
        expr: rate(payment_webhook_signature_failures_total[5m]) > 0
        for: 1m
        annotations:
          summary: "Webhook signature validation failures detected"
          description: "Provider {{ $labels.provider }}: {{ $labels.reason }}"
          
      # High severity suspicious activity
      - alert: SuspiciousActivityHighSeverity
        expr: rate(payment_suspicious_activity_alerts_total{severity="high"}[5m]) > 0
        for: 1m
        annotations:
          summary: "High severity suspicious activity detected"
          description: "Type: {{ $labels.type }}, Severity: {{ $labels.severity }}"
          
  - name: payment_tap_to_pay_alerts
    rules:
      - alert: HighTapToPayFailureRate
        expr: rate(payment_tap_to_pay_failures_total[5m]) > 0.05
        for: 5m
        annotations:
          summary: "High Tap-to-Pay failure rate detected"
          
      - alert: TapToPayReplayAttempts
        expr: rate(payment_tap_to_pay_replay_attempts_total[5m]) > 0
        for: 1m
        annotations:
          summary: "Tap-to-Pay replay attempts detected"
          
      - alert: HighTapToPayLatency
        expr: histogram_quantile(0.95, payment_tap_to_pay_duration_seconds) > 5
        for: 5m
        annotations:
          summary: "High Tap-to-Pay latency (P95 > 5s)"
```

## See Also

- [Kubernetes Deployment](Kubernetes_Deployment.md)
- [Performance & Optimization](Performance_Optimization.md)
- [Reporting Module](../02-Payment/Reporting_Module.md)
- [Tap-to-Pay Integration](../02-Payment/TapToPay_Integration.md)

