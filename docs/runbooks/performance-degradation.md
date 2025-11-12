# Performance Degradation Runbook

## Incident Type and Severity

- **Type**: Performance Degradation
- **Severity**: Medium to High
- **Impact**: Slow response times, timeouts, user experience degradation
- **Detection**: High response times, increased error rates, CPU/memory spikes

## Prerequisites and Access Requirements

- Kubernetes cluster access (`kubectl`)
- Monitoring dashboard access
- Database access (read-only for diagnostics)
- Application logs access

## Step-by-Step Procedures

### 1. Verify Performance Metrics

```bash
# Check response time metrics
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep http_request_duration

# Check error rates
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep http_errors

# Check CPU and memory usage
kubectl top pods -l app=payment-api
```

### 2. Identify Affected Endpoints

```bash
# Check endpoint-specific metrics
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep endpoint_duration

# Review slow request logs
kubectl logs -f deployment/payment-api | grep -i "slow.*request"
```

### 3. Check Database Performance

```bash
# Check database query performance
kubectl exec -it deployment/postgresql -- psql -U postgres -c "SELECT query, mean_exec_time FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;"

# Check database connections
kubectl exec -it deployment/postgresql -- psql -U postgres -c "SELECT count(*) FROM pg_stat_activity;"

# Check for long-running queries
kubectl exec -it deployment/postgresql -- psql -U postgres -c "SELECT pid, now() - pg_stat_activity.query_start AS duration, query FROM pg_stat_activity WHERE state = 'active' AND now() - pg_stat_activity.query_start > interval '5 minutes';"
```

### 4. Check External Dependencies

```bash
# Check payment provider response times
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep provider_response_time

# Check cache hit rates
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep cache_hit_rate
```

### 5. Review Recent Changes

- Check recent deployments
- Review configuration changes
- Check for feature flag changes
- Review database migrations

### 6. Scale Application (if resource constrained)

```bash
# Scale up application pods
kubectl scale deployment payment-api --replicas=5

# Monitor recovery
watch -n 5 'kubectl top pods -l app=payment-api'
```

### 7. Enable Caching (if disabled)

```bash
# Check cache configuration
kubectl get configmap payment-config -o yaml | grep -i cache

# Enable caching if needed
kubectl set env deployment/payment-api CACHE_ENABLED=true
```

### 8. Optimize Database Queries

If database is the bottleneck:

1. Review slow query log
2. Add missing indexes
3. Optimize query patterns
4. Consider read replicas for read-heavy workloads

## Rollback Procedures

If recent changes caused degradation:

1. Revert deployment to previous version
2. Restore previous configuration
3. Rollback database migrations (if applicable)

## Escalation Paths

- **Level 1 (0-30 min)**: On-call engineer
- **Level 2 (30-60 min)**: Performance engineer
- **Level 3 (60+ min)**: Engineering manager

## Contact Information

- **On-Call Engineer**: Check PagerDuty
- **Performance Team**: performance@example.com
- **Database Admin**: db-admin@example.com

## Related Monitoring Dashboards

- Application Performance: `/d/app-performance`
- Database Performance: `/d/db-performance`
- Response Times: `/d/response-times`

## Common Performance Issues

### Database Query Performance
- **Symptom**: Slow database queries
- **Solution**: Add indexes, optimize queries, use read replicas

### External API Latency
- **Symptom**: Slow payment provider responses
- **Solution**: Implement caching, use circuit breakers, optimize retry logic

### Memory Leaks
- **Symptom**: Increasing memory usage over time
- **Solution**: Review memory usage patterns, fix leaks, increase memory limits

### CPU Spikes
- **Symptom**: High CPU usage
- **Solution**: Profile application, optimize hot paths, scale horizontally

## Post-Incident Actions

1. Conduct performance analysis
2. Identify root cause
3. Implement optimizations
4. Update performance benchmarks
5. Schedule performance review

