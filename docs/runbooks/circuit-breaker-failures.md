# Circuit Breaker Failures Runbook

## Incident Type and Severity

- **Type**: Circuit Breaker Failures
- **Severity**: Medium to High
- **Impact**: Payment processing failures, degraded service
- **Detection**: Circuit breaker stuck open, high failure rates, no automatic recovery

## Prerequisites and Access Requirements

- Kubernetes cluster access (`kubectl`)
- Admin panel access
- Monitoring dashboard access
- Provider status information

## Step-by-Step Procedures

### 1. Verify Circuit Breaker Status

```bash
# Check circuit breaker metrics
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep circuit_breaker

# Check circuit breaker state
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/health/circuit-breaker
```

### 2. Identify Affected Provider

```bash
# Check which provider's circuit breaker is open
kubectl logs -f deployment/payment-api | grep -i "circuit.*breaker.*open"

# Check provider failure rates
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep provider_failure_rate
```

### 3. Verify Provider Status

- Check provider status page
- Verify provider API health endpoint
- Check recent provider API changes

### 4. Manual Circuit Breaker Reset (if needed)

If circuit breaker is stuck open and provider has recovered:

1. Access admin panel: `https://payment-api/admin/circuit-breakers`
2. Select affected provider
3. Click "Reset Circuit Breaker"
4. Monitor for recovery

### 5. Test Provider Connection

```bash
# Test provider endpoint (if accessible)
kubectl exec -it deployment/payment-api -- curl -v https://api.{provider}.com/health

# Check provider response times
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep provider_response_time
```

### 6. Switch to Backup Provider

If provider is confirmed down:

1. Disable affected provider in admin panel
2. Enable backup provider
3. Update routing configuration
4. Monitor payment success rate

### 7. Adjust Circuit Breaker Thresholds (if needed)

If circuit breaker is too sensitive:

1. Access configuration: `kubectl edit configmap payment-config`
2. Adjust failure threshold (default: 5 failures)
3. Adjust timeout duration (default: 30 seconds)
4. Restart pods: `kubectl rollout restart deployment/payment-api`

## Rollback Procedures

If manual reset causes issues:

1. Revert circuit breaker state
2. Restore original configuration
3. Re-enable automatic circuit breaker

## Escalation Paths

- **Level 1 (0-15 min)**: On-call engineer
- **Level 2 (15-30 min)**: Payment operations lead
- **Level 3 (30+ min)**: Engineering manager

## Contact Information

- **On-Call Engineer**: Check PagerDuty
- **Payment Operations**: payment-ops@example.com
- **Provider Support**: See provider-specific contacts

## Related Monitoring Dashboards

- Circuit Breaker Status: `/d/circuit-breakers`
- Provider Health: `/d/payment-providers`
- Failure Rates: `/d/failure-rates`

## Circuit Breaker Configuration

Default settings:
- **Failure Threshold**: 5 consecutive failures
- **Timeout**: 30 seconds
- **Half-Open Retry**: 1 request
- **Success Threshold**: 2 successful requests

## Post-Incident Actions

1. Review circuit breaker configuration
2. Analyze failure patterns
3. Adjust thresholds if needed
4. Update provider health checks

