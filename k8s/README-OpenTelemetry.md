# OpenTelemetry Configuration for Production

This document describes how to configure OpenTelemetry with Jaeger and Zipkin for distributed tracing in the Payment Microservice.

## Overview

The Payment Microservice uses **OpenTelemetry** for distributed tracing, which enables:
- **End-to-end request tracing** across all microservices
- **Performance analysis** and bottleneck identification
- **Error correlation** between logs and traces
- **Distributed debugging** across the microservices architecture

## Architecture

```
┌─────────────────┐         ┌─────────────────┐         ┌─────────────────┐
│ Payment API   │──────────▶│  Jaeger Agent    │──────────▶│  Jaeger UI      │
│ (OpenTelemetry│  UDP:6831 │  (UDP Collector) │           │  (Port 16686)    │
│  Traces)      │           └─────────────────┘           └─────────────────┘
└─────────────────┘
         │
         │ HTTP:9411
         ▼
┌─────────────────┐
│  Zipkin         │
│  (Alternative)  │
└─────────────────┘
```

## Deployment

### Option 1: Jaeger (Recommended)

1. **Deploy Jaeger:**
   ```bash
   kubectl apply -f k8s/jaeger-deployment.yaml
   ```

2. **Access Jaeger UI:**
   - Port-forward: `kubectl port-forward -n payment svc/jaeger-query 16686:16686`
   - Or use LoadBalancer: `http://<jaeger-query-service-ip>:16686`

3. **Verify Configuration:**
   - Check that `jaeger-agent` service is accessible from payment pods
   - Verify environment variables in payment deployment

### Option 2: Zipkin (Alternative)

1. **Deploy Zipkin:**
   ```bash
   kubectl apply -f k8s/zipkin-deployment.yaml
   ```

2. **Access Zipkin UI:**
   - Port-forward: `kubectl port-forward -n payment svc/zipkin 9411:9411`
   - Or use LoadBalancer: `http://<zipkin-service-ip>:9411`

3. **Update ConfigMap:**
   - Set `OpenTelemetry__Zipkin__Endpoint` to Zipkin service URL

## Configuration

### Production Configuration

The OpenTelemetry configuration follows Clean Architecture principles and is configured via extension methods in `src/Payment.API/Extensions/OpenTelemetryExtensions.cs`.

### Kubernetes ConfigMap

The OpenTelemetry configuration is stored in `k8s/configmap.yaml`:

```yaml
data:
  OpenTelemetry__UseConsoleExporter: "false"
  OpenTelemetry__ServiceName: "Payment.API"
  OpenTelemetry__ServiceVersion: "1.0.0"
  OpenTelemetry__ServiceNamespace: "payment"
  OpenTelemetry__SamplingRatio: "0.1"  # 10% sampling in production
  OpenTelemetry__Jaeger__Host: "jaeger-agent"
  OpenTelemetry__Jaeger__Port: "6831"
  OpenTelemetry__Zipkin__Endpoint: "http://zipkin:9411/api/v2/spans"
  OpenTelemetry__Otlp__Endpoint: ""  # Optional: OTLP endpoint
```

### Environment Variables

The payment deployment includes environment variables:

```yaml
env:
  - name: JAEGER_AGENT_HOST
    value: "jaeger-agent"
  - name: JAEGER_AGENT_PORT
    value: "6831"
  - name: ZIPKIN_ENDPOINT
    value: "http://zipkin:9411/api/v2/spans"
  - name: OTLP_ENDPOINT
    value: ""  # Optional: OTLP endpoint for modern collectors
  - name: KUBERNETES_NAMESPACE
    valueFrom:
      fieldRef:
        fieldPath: metadata.namespace
  - name: HOSTNAME
    valueFrom:
      fieldRef:
        fieldPath: metadata.name
```

### Production Features

- **Automatic Sampling**: 10% sampling rate in production to reduce overhead
- **Resource Attributes**: Automatic K8s metadata (pod name, namespace) in traces
- **Batch Export**: Optimized batch processing with configurable queue sizes
- **Multiple Exporters**: Support for Jaeger, Zipkin, and OTLP simultaneously
- **Error Handling**: Graceful degradation if exporters are unavailable
- **Health Checks**: Jaeger and Zipkin deployments include liveness/readiness probes

## Local Development

For local development, use Docker Compose:

```bash
docker-compose up -d
```

This will start:
- **Jaeger UI**: `http://localhost:16686`
- **Zipkin UI**: `http://localhost:9411`
- **Payment API**: `http://localhost:5000`

## Verification

### Check Traces in Jaeger

1. Open Jaeger UI: `http://localhost:16686` (local) or port-forward in K8s
2. Select service: `Payment.API`
3. Click "Find Traces"
4. You should see traces for payment operations

### Check Traces in Zipkin

1. Open Zipkin UI: `http://localhost:9411` (local) or port-forward in K8s
2. Click "Run Query"
3. Select service: `Payment.API`
4. You should see traces for payment operations

## Custom Spans

The following handlers create custom spans:

- **CreatePayment**: Payment creation with provider, amount, currency tags
- **GetPaymentById**: Payment retrieval with cache hit/miss tags
- **FailPayment**: Payment failure with reason tags
- **RefundPayment**: Payment refund with transaction ID tags

## Correlation IDs

All log entries automatically include:
- **TraceId**: W3C trace identifier
- **SpanId**: W3C span identifier

Example log entry:
```
[2024-01-15 10:30:00.123] [INF] [TraceId: abc123...] [SpanId: def456...] Creating payment for order order-456
```

## Troubleshooting

### Traces Not Appearing

1. **Check Jaeger/Zipkin is running:**
   ```bash
   kubectl get pods -n payment | grep -E "jaeger|zipkin"
   ```

2. **Check service connectivity:**
   ```bash
   kubectl exec -n payment <payment-pod> -- nslookup jaeger-agent
   ```

3. **Check OpenTelemetry configuration:**
   ```bash
   kubectl exec -n payment <payment-pod> -- env | grep -i opentelemetry
   ```

4. **Check logs for errors:**
   ```bash
   kubectl logs -n payment <payment-pod> | grep -i "opentelemetry\|jaeger\|zipkin"
   ```

### Performance Impact

OpenTelemetry has minimal performance impact:
- **Sampling**: Configure sampling rate in `Program.cs` if needed
- **Async Export**: Traces are exported asynchronously
- **Batching**: Multiple spans are batched before export

## Production Recommendations

1. **Sampling Configuration**: 
   - Production uses 10% sampling (configurable via `OpenTelemetry__SamplingRatio`)
   - Adjust based on traffic volume and storage capacity
   - Higher sampling for critical paths, lower for routine operations

2. **Storage Backend**:
   - **Jaeger**: Currently using BadgerDB (in-memory with persistence option)
   - For production at scale, consider Elasticsearch or Cassandra backend
   - Configure retention policies via `BADGER_SPAN_STORE_TTL` (default: 7 days)

3. **Resource Limits**:
   - Jaeger: 1Gi memory limit, 1 CPU limit
   - Zipkin: 512Mi memory limit, 500m CPU limit
   - Adjust based on trace volume and cluster capacity

4. **Persistence**:
   - For production, replace `emptyDir` with PersistentVolumeClaim in `jaeger-deployment.yaml`
   - Ensures trace data survives pod restarts

5. **Monitoring**:
   - Monitor trace export metrics to detect issues
   - Set up alerts for exporter failures
   - Track sampling rates and adjust as needed

6. **Multi-Environment**:
   - Use separate Jaeger/Zipkin instances for different environments
   - Configure different service names per environment
   - Use namespace isolation in Kubernetes

7. **OTLP Support**:
   - Modern standard for trace export
   - Supports both gRPC and HTTP protocols
   - Compatible with OpenTelemetry Collector for advanced routing

## Additional Resources

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/instrumentation/net/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Zipkin Documentation](https://zipkin.io/)

