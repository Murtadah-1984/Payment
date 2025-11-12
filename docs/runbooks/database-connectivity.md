# Database Connectivity Issues Runbook

## Incident Type and Severity

- **Type**: Database Connectivity Issues
- **Severity**: Critical
- **Impact**: Complete service unavailability
- **Detection**: Health check failures, connection pool exhaustion, timeout errors

## Prerequisites and Access Requirements

- Kubernetes cluster access (`kubectl`)
- Database admin access (read-only for diagnostics)
- Monitoring dashboard access
- Database connection string (from secrets)

## Step-by-Step Procedures

### 1. Verify Database Status

```bash
# Check database health check
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/health/ready

# Check database pod status
kubectl get pods -l app=postgresql

# Check database logs
kubectl logs -f deployment/postgresql
```

### 2. Check Connection Pool

```bash
# Check active connections
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep db_connections

# Check connection pool metrics
kubectl exec -it deployment/payment-api -- curl http://localhost:8080/metrics | grep connection_pool
```

### 3. Verify Network Connectivity

```bash
# Test database connectivity from pod
kubectl exec -it deployment/payment-api -- nc -zv postgresql-service 5432

# Check DNS resolution
kubectl exec -it deployment/payment-api -- nslookup postgresql-service
```

### 4. Check Database Resources

```bash
# Check database CPU and memory
kubectl top pod -l app=postgresql

# Check disk space
kubectl exec -it deployment/postgresql -- df -h

# Check database connections
kubectl exec -it deployment/postgresql -- psql -U postgres -c "SELECT count(*) FROM pg_stat_activity;"
```

### 5. Review Recent Changes

- Check recent deployments
- Review configuration changes
- Check secret updates

### 6. Restart Database Pod (if needed)

```bash
# Restart database pod (last resort)
kubectl delete pod -l app=postgresql

# Wait for pod to restart
kubectl get pods -l app=postgresql -w
```

### 7. Scale Application (if connection pool exhausted)

```bash
# Scale down to reduce connection pressure
kubectl scale deployment payment-api --replicas=2

# Monitor recovery
watch -n 5 'kubectl get pods -l app=payment-api'
```

## Rollback Procedures

If changes were made:

1. Revert configuration changes
2. Restore previous deployment version
3. Restore database from backup if data corruption suspected

## Escalation Paths

- **Level 1 (0-5 min)**: On-call engineer
- **Level 2 (5-15 min)**: Database administrator
- **Level 3 (15+ min)**: Engineering manager + CTO

## Contact Information

- **On-Call Engineer**: Check PagerDuty
- **Database Admin**: db-admin@example.com
- **Infrastructure Team**: infra@example.com

## Related Monitoring Dashboards

- Database Health: `/d/database-health`
- Connection Pool: `/d/connection-pool`
- Database Performance: `/d/db-performance`

## Common Issues and Solutions

### Connection Pool Exhausted
- **Symptom**: "Connection pool exhausted" errors
- **Solution**: Scale down application, check for connection leaks, increase pool size

### Network Partition
- **Symptom**: Timeout errors, DNS resolution failures
- **Solution**: Check network policies, verify service endpoints

### Database Overload
- **Symptom**: Slow queries, high CPU/memory
- **Solution**: Check for long-running queries, optimize indexes, scale database

## Post-Incident Actions

1. Review connection pool configuration
2. Analyze connection leak patterns
3. Update connection pool settings if needed
4. Schedule database optimization review

