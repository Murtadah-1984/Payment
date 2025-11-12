# Security Breach (Unauthorized Access) Runbook

## Incident Type and Severity

- **Type**: Security Breach / Unauthorized Access
- **Severity**: Critical
- **Impact**: Data breach, compliance violations, service compromise
- **Detection**: Unusual access patterns, failed authentication spikes, security alerts

## Prerequisites and Access Requirements

- Security admin access
- Audit log access
- Database access (read-only for forensics)
- Incident management system access
- Legal/compliance team contact

## Step-by-Step Procedures

### 1. Immediate Containment

```bash
# Revoke all active sessions
kubectl exec -it deployment/payment-api -- curl -X POST http://localhost:8080/admin/security/revoke-all-sessions

# Block suspicious IP addresses
kubectl exec -it deployment/payment-api -- curl -X POST http://localhost:8080/admin/security/block-ip -d '{"ipAddress":"<suspicious-ip>"}'

# Enable enhanced logging
kubectl set env deployment/payment-api LOG_LEVEL=Debug
```

### 2. Identify Compromised Accounts

```bash
# Query audit logs for suspicious activity
kubectl exec -it deployment/payment-api -- curl "http://localhost:8080/admin/audit-logs?eventType=UnauthorizedAccess&startTime=<timestamp>"

# Check for failed authentication attempts
kubectl logs -f deployment/payment-api | grep -i "authentication.*failed"
```

### 3. Revoke Compromised Credentials

1. Access admin panel: `https://payment-api/admin/security/credentials`
2. Identify compromised accounts
3. Revoke credentials immediately
4. Force password reset for affected users

### 4. Preserve Evidence

```bash
# Export audit logs
kubectl exec -it deployment/payment-api -- curl "http://localhost:8080/admin/audit-logs/export/json?startTime=<timestamp>" -o audit-logs.json

# Export security events
kubectl exec -it deployment/payment-api -- curl "http://localhost:8080/admin/audit-logs/security-events?startTime=<timestamp>" -o security-events.json

# Backup database (for forensics)
kubectl exec -it deployment/postgresql -- pg_dump -U postgres payment_db > security-incident-backup.sql
```

### 5. Notify Stakeholders

**Immediate notifications (within 1 hour):**
- Security team lead
- Engineering manager
- Legal/compliance team
- CISO

**If data breach confirmed:**
- Notify affected users (per compliance requirements)
- Report to regulatory authorities (if required)
- Prepare public statement (if needed)

### 6. Assess Impact

- Review accessed data
- Identify exposed PII/PCI data
- Determine scope of breach
- Document timeline

### 7. Implement Additional Security Measures

```bash
# Enable rate limiting
kubectl set env deployment/payment-api RATE_LIMIT_ENABLED=true

# Enable IP whitelisting for admin endpoints
kubectl set env deployment/payment-api ADMIN_IP_WHITELIST_ENABLED=true

# Increase authentication requirements
kubectl set env deployment/payment-api REQUIRE_MFA=true
```

## Rollback Procedures

If containment actions cause service disruption:

1. Restore service access for legitimate users
2. Whitelist legitimate IP addresses
3. Restore normal logging levels (after evidence collection)

## Escalation Paths

- **Level 1 (0-5 min)**: Security on-call engineer
- **Level 2 (5-15 min)**: Security team lead + CISO
- **Level 3 (15+ min)**: Executive team + Legal + Compliance

## Contact Information

- **Security On-Call**: security-oncall@example.com
- **Security Team Lead**: security-lead@example.com
- **CISO**: ciso@example.com
- **Legal Team**: legal@example.com
- **Compliance Team**: compliance@example.com

## Related Monitoring Dashboards

- Security Events: `/d/security-events`
- Authentication Failures: `/d/auth-failures`
- Unauthorized Access: `/d/unauthorized-access`

## Compliance Requirements

- **PCI DSS**: Report within 24 hours if card data accessed
- **GDPR**: Report within 72 hours if EU data accessed
- **SOC 2**: Document incident and remediation

## Post-Incident Actions

1. Conduct full security audit
2. Review access controls
3. Update security policies
4. Schedule security training
5. Implement additional monitoring
6. Prepare incident report for compliance

## Prevention Measures

- Regular security audits
- Penetration testing
- Access control reviews
- Security awareness training
- Threat intelligence monitoring

