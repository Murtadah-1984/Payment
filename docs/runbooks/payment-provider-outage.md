# Payment Provider Outage Runbook

## Incident Type and Severity

- **Type**: Payment Provider Outage
- **Severity**: High
- **Impact**: Payment processing failures, revenue loss
- **Detection**: Circuit breaker opens, high failure rates, provider status page

## Prerequisites and Access Requirements

- Access to Payment Microservice admin panel
- Kubernetes cluster access (`kubectl`)
- Database access (read-only for diagnostics)
- Monitoring dashboard access (Grafana/CloudWatch)
- Provider status page access

## Step-by-Step Procedures

### 1. Verify Incident

```bash
# Check circuit breaker status
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/health/circuit-breaker

# Check provider health endpoint
kubectl logs -f deployment/payment-api | grep -i "circuit.*breaker"

# Verify provider status page
# Check: https://status.{provider}.com
```

### 2. Activate Circuit Breaker (if not already open)

The circuit breaker should automatically open after failure threshold. Verify:

```bash
# Check circuit breaker metrics
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep circuit_breaker
```

### 3. Switch to Backup Provider

If automatic failover is not working:

1. Access admin panel: `https://payment-api/admin/providers`
2. Disable affected provider
3. Enable backup provider
4. Verify routing configuration

### 4. Monitor Payment Success Rate

```bash
# Watch payment success metrics
watch -n 5 'kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep payment_success_rate'
```

### 5. Notify Stakeholders

- Send incident notification to:
  - Payment operations team
  - Customer support team
  - Business stakeholders
- Include: Provider name, estimated duration, workaround status

### 6. Document Incident

- Log incident in incident management system
- Record: Start time, affected provider, actions taken, resolution time

## Rollback Procedures

If switching providers causes issues:

1. Revert provider configuration to original state
2. Re-enable original provider (if it recovers)
3. Monitor for stability
4. Update routing rules if needed

## Escalation Paths

- **Level 1 (0-15 min)**: On-call engineer
- **Level 2 (15-30 min)**: Payment operations lead
- **Level 3 (30+ min)**: Engineering manager + CTO

## Contact Information

- **On-Call Engineer**: Check PagerDuty
- **Payment Operations**: payment-ops@example.com
- **Provider Support**: See provider-specific contacts in `docs/providers/`

## Related Monitoring Dashboards

- Payment Provider Health: `/d/payment-providers`
- Circuit Breaker Status: `/d/circuit-breakers`
- Payment Success Rates: `/d/payment-metrics`

## Post-Incident Actions

1. Review incident timeline
2. Identify root cause
3. Update runbook with learnings
4. Schedule post-mortem meeting
5. Implement preventive measures

