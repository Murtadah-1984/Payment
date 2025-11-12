# Rate Limiting Incidents Runbook

## Incident Type and Severity

- **Type**: Rate Limiting Incidents
- **Severity**: Low to Medium
- **Impact**: Legitimate users blocked, API availability issues
- **Detection**: High rate limit rejection rate, user complaints, 429 errors

## Prerequisites and Access Requirements

- Kubernetes cluster access (`kubectl`)
- Admin panel access
- Monitoring dashboard access
- Rate limiting configuration access

## Step-by-Step Procedures

### 1. Verify Rate Limit Status

```bash
# Check rate limit rejection metrics
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep rate_limit_rejections

# Check rate limit configuration
kubectl get configmap payment-config -o yaml | grep -i rate_limit

# Check recent rate limit violations
kubectl logs -f deployment/payment-api | grep -i "rate.*limit.*exceeded"
```

### 2. Identify Affected Users/IPs

```bash
# Check which IPs are being rate limited
kubectl logs -f deployment/payment-api | grep -i "rate.*limit" | awk '{print $NF}' | sort | uniq -c | sort -rn

# Check user-specific rate limits
kubectl exec -it deployment/payment-api -- curl "http://localhost:8080/admin/rate-limits/status"
```

### 3. Determine if Legitimate Traffic

- Review request patterns
- Check for DDoS indicators
- Verify user authentication
- Review API usage patterns

### 4. Adjust Rate Limits (if too restrictive)

```bash
# Update rate limit configuration
kubectl edit configmap payment-config

# Adjust limits (example):
# - IP-based: 100 requests/minute
# - User-based: 1000 requests/minute
# - Endpoint-specific: Adjust per endpoint

# Restart pods to apply changes
kubectl rollout restart deployment/payment-api
```

### 5. Whitelist Legitimate IPs (if needed)

```bash
# Add IP to whitelist
kubectl set env deployment/payment-api RATE_LIMIT_WHITELIST="<ip1>,<ip2>"

# Or update configmap
kubectl edit configmap payment-config
```

### 6. Implement Distributed Rate Limiting (if using Redis)

```bash
# Verify Redis connectivity
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/health/ready | grep redis

# Check Redis rate limit keys
kubectl exec -it deployment/redis -- redis-cli KEYS "ratelimit:*"
```

### 7. Monitor Recovery

```bash
# Watch rate limit metrics
watch -n 5 'kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep rate_limit'
```

## Rollback Procedures

If rate limit changes cause issues:

1. Restore previous configuration
2. Revert whitelist changes
3. Restart pods

## Escalation Paths

- **Level 1 (0-30 min)**: On-call engineer
- **Level 2 (30-60 min)**: Payment operations lead
- **Level 3 (60+ min)**: Engineering manager

## Contact Information

- **On-Call Engineer**: Check PagerDuty
- **Payment Operations**: payment-ops@example.com

## Related Monitoring Dashboards

- Rate Limiting: `/d/rate-limiting`
- API Usage: `/d/api-usage`
- Rejection Rates: `/d/rejection-rates`

## Rate Limit Configuration

Default settings:
- **IP-based**: 100 requests/minute
- **User-based**: 1000 requests/minute
- **Burst**: 20 requests
- **Window**: 60 seconds

## Common Issues and Solutions

### Legitimate Users Blocked
- **Symptom**: High false positive rate
- **Solution**: Increase rate limits, implement user-based limits

### DDoS Attack
- **Symptom**: Sudden spike in requests from multiple IPs
- **Solution**: Enable IP-based rate limiting, block malicious IPs

### Distributed Rate Limiting Not Working
- **Symptom**: Rate limits not enforced across instances
- **Solution**: Verify Redis connectivity, check distributed cache configuration

## Post-Incident Actions

1. Review rate limit thresholds
2. Analyze request patterns
3. Adjust limits based on usage
4. Implement adaptive rate limiting (if needed)

