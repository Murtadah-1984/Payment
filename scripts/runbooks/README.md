# Runbook Automation Scripts

This directory contains automation scripts to support incident response runbooks for the Payment Microservice.

## Scripts

### 1. Kubernetes Diagnostics (`kubernetes-diagnostics.sh`)

Comprehensive Kubernetes cluster diagnostics for the Payment Microservice.

**Usage:**
```bash
./kubernetes-diagnostics.sh [namespace] [deployment-name]
```

**Example:**
```bash
./kubernetes-diagnostics.sh payment payment-api
```

**What it checks:**
- Deployment status
- Pod status and resource usage
- Recent pod events
- Service and ingress status
- ConfigMap and Secret status
- HPA status
- Pod logs (last 50 lines)
- Health check endpoints
- Circuit breaker status

### 2. Database Queries (`database-queries.sh`)

Database diagnostic queries for troubleshooting payment and audit data.

**Usage:**
```bash
./database-queries.sh [query-type] [connection-string]
```

**Query Types:**
- `health` - Database health and connection status
- `payments` - Payment statistics and recent failures
- `failures` - Payment failure analysis
- `audit` - Audit log entries and security events
- `performance` - Query performance and table sizes

**Example:**
```bash
# Health check
./database-queries.sh health "postgresql://user:pass@host:5432/paymentdb"

# Payment failures
./database-queries.sh failures "postgresql://user:pass@host:5432/paymentdb"
```

**Prerequisites:**
- `psql` command-line tool installed
- Valid PostgreSQL connection string
- Appropriate database permissions

### 3. Log Analysis (`log-analysis.sh`)

Analyzes application logs for errors, patterns, and anomalies.

**Usage:**
```bash
./log-analysis.sh [namespace] [deployment-name] [time-range]
```

**Time Ranges:**
- `1h` - Last hour (default)
- `6h` - Last 6 hours
- `24h` - Last 24 hours

**Example:**
```bash
# Last hour
./log-analysis.sh payment payment-api 1h

# Last 24 hours
./log-analysis.sh payment payment-api 24h
```

**What it analyzes:**
- Error count by type
- Payment processing errors
- Circuit breaker events
- Provider errors
- Security events
- Webhook events
- High frequency errors
- Request latency warnings
- Critical errors
- Log summary statistics

### 4. Health Check (`health-check.sh`)

Comprehensive health check for all pods in the deployment.

**Usage:**
```bash
./health-check.sh [namespace] [deployment-name] [endpoint]
```

**Example:**
```bash
./health-check.sh payment payment-api http://localhost:8080
```

**What it checks:**
- Basic health endpoint
- Readiness endpoint
- Liveness endpoint
- Circuit breaker status
- Database connectivity
- Payment provider health
- Deployment status
- Pod status summary

## Setup

### Make Scripts Executable

```bash
chmod +x scripts/runbooks/*.sh
```

### Prerequisites

1. **kubectl** - Kubernetes command-line tool
   - Must be configured with cluster access
   - Must have permissions to access the target namespace

2. **psql** - PostgreSQL client (for database queries)
   - Install: `apt-get install postgresql-client` or `brew install postgresql`

3. **jq** - JSON processor (optional, for better output formatting)
   - Install: `apt-get install jq` or `brew install jq`

### Configuration

Scripts use default values but can be customized:

- **Namespace**: Default `payment`
- **Deployment**: Default `payment-api`
- **Endpoint**: Default `http://localhost:8080`
- **Time Range**: Default `1h` (for log analysis)

## Integration with Runbooks

These scripts are referenced in the incident response runbooks:

- **Payment Provider Outage** - Uses `kubernetes-diagnostics.sh` and `health-check.sh`
- **Database Connectivity** - Uses `database-queries.sh` and `kubernetes-diagnostics.sh`
- **Circuit Breaker Failures** - Uses `log-analysis.sh` and `health-check.sh`
- **Security Breach** - Uses `log-analysis.sh` and `database-queries.sh audit`
- **Performance Degradation** - Uses `log-analysis.sh` and `database-queries.sh performance`

## Troubleshooting

### Script Fails with "kubectl not found"
- Install kubectl: https://kubernetes.io/docs/tasks/tools/
- Ensure kubectl is in your PATH

### Script Fails with "psql not found"
- Install PostgreSQL client tools
- Ensure psql is in your PATH

### Cannot Connect to Database
- Verify connection string format: `postgresql://user:pass@host:port/database`
- Check network connectivity
- Verify database credentials and permissions

### No Pods Found
- Verify namespace exists: `kubectl get namespace <namespace>`
- Verify deployment exists: `kubectl get deployment -n <namespace>`
- Check pod labels match: `kubectl get pods -n <namespace> -l app=<deployment>`

## Security Considerations

- Scripts may contain sensitive information (connection strings, credentials)
- Store connection strings securely (environment variables, secrets)
- Use read-only database credentials for diagnostic queries
- Review script outputs before sharing (may contain sensitive data)
- Scripts should be run by authorized personnel only

## Contributing

When adding new scripts:

1. Follow the existing script structure
2. Include error handling and validation
3. Add usage documentation
4. Test with actual cluster/database
5. Update this README with script details

