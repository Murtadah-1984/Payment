# Webhook Signature Validation Failures Runbook

## Incident Type and Severity

- **Type**: Webhook Signature Validation Failures
- **Severity**: Medium
- **Impact**: Payment status updates not received, reconciliation issues
- **Detection**: High webhook validation failure rate, missing payment status updates

## Prerequisites and Access Requirements

- Kubernetes cluster access (`kubectl`)
- Admin panel access
- Provider documentation access
- Webhook configuration access

## Step-by-Step Procedures

### 1. Verify Failure Rate

```bash
# Check webhook validation failure metrics
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep webhook_validation_failures

# Check webhook delivery logs
kubectl logs -f deployment/payment-api | grep -i "webhook.*signature.*invalid"
```

### 2. Identify Affected Provider

```bash
# Check which provider's webhooks are failing
kubectl logs -f deployment/payment-api | grep -i "webhook.*signature.*failed" | grep -i provider

# Check provider-specific webhook configuration
kubectl get configmap payment-config -o yaml | grep -i webhook
```

### 3. Verify Webhook Secret

1. Access secrets: `kubectl get secret payment-secrets -o yaml`
2. Verify webhook secret matches provider configuration
3. Check for secret rotation (if provider rotated secret)

### 4. Test Webhook Signature

```bash
# Test webhook signature validation (if test endpoint available)
kubectl exec -it deployment/payment-api -- curl -X POST http://localhost:8080/admin/webhooks/test-signature \
  -d '{"provider":"<provider>","payload":"<test-payload>","signature":"<test-signature>"}'
```

### 5. Update Webhook Secret (if needed)

If provider rotated secret:

1. Get new secret from provider dashboard
2. Update Kubernetes secret:
   ```bash
   kubectl create secret generic payment-secrets \
     --from-literal=WebhookSecret_<Provider>=<new-secret> \
     --dry-run=client -o yaml | kubectl apply -f -
   ```
3. Restart pods: `kubectl rollout restart deployment/payment-api`

### 6. Verify Provider Webhook Configuration

- Check provider dashboard for webhook URL
- Verify webhook URL matches service endpoint
- Check webhook signature algorithm (HMAC-SHA256, etc.)
- Verify webhook events are enabled

### 7. Review Recent Changes

- Check recent deployments
- Review webhook configuration changes
- Check secret updates

### 8. Enable Webhook Retry (if disabled)

```bash
# Check webhook retry configuration
kubectl get configmap payment-config -o yaml | grep -i webhook_retry

# Enable retry if needed
kubectl set env deployment/payment-api WEBHOOK_RETRY_ENABLED=true
```

## Rollback Procedures

If secret update causes issues:

1. Restore previous secret
2. Revert configuration changes
3. Restart pods

## Escalation Paths

- **Level 1 (0-30 min)**: On-call engineer
- **Level 2 (30-60 min)**: Payment operations lead
- **Level 3 (60+ min)**: Engineering manager

## Contact Information

- **On-Call Engineer**: Check PagerDuty
- **Payment Operations**: payment-ops@example.com
- **Provider Support**: See provider-specific contacts

## Related Monitoring Dashboards

- Webhook Delivery: `/d/webhook-delivery`
- Signature Validation: `/d/webhook-validation`
- Payment Status Updates: `/d/payment-status`

## Common Issues and Solutions

### Secret Mismatch
- **Symptom**: All webhooks failing validation
- **Solution**: Update webhook secret to match provider

### Algorithm Mismatch
- **Symptom**: Validation fails with correct secret
- **Solution**: Verify signature algorithm matches provider (HMAC-SHA256, etc.)

### Payload Format Changes
- **Symptom**: Validation fails after provider update
- **Solution**: Review provider changelog, update signature calculation

## Post-Incident Actions

1. Review webhook secret rotation process
2. Document provider-specific webhook requirements
3. Implement automated secret rotation (if possible)
4. Add webhook signature validation tests

