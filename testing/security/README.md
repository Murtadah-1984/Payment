# Security Testing

This directory contains security testing scripts and tools for the Payment Microservice, including OWASP ZAP scans and penetration testing.

## Prerequisites

### OWASP ZAP

**Option 1: Docker (Recommended)**
```bash
docker pull owasp/zap2docker-stable
```

**Option 2: Local Installation**
- Download from [OWASP ZAP Downloads](https://www.zaproxy.org/download/)
- Or use package manager:
  ```bash
  # macOS
  brew install --cask owasp-zap
  
  # Linux
  sudo apt-get install zaproxy
  ```

### Required Tools

- `curl` - HTTP client
- `jq` - JSON processor (for parsing ZAP results)
- `bash` - Shell script interpreter

## Test Scripts

### 1. Baseline Scan (`owasp-zap-baseline.sh`)

Runs a quick baseline security scan:
- **Duration**: ~5-10 minutes
- **Coverage**: Unauthenticated endpoints
- **Output**: HTML, JSON, and XML reports

**Run:**
```bash
# Start ZAP
docker-compose -f testing/security/docker-compose.zap.yml up -d

# Wait for ZAP to be ready
sleep 10

# Run baseline scan
chmod +x testing/security/owasp-zap-baseline.sh
API_URL=http://localhost:5000 ./testing/security/owasp-zap-baseline.sh
```

### 2. Full Scan (`owasp-zap-full-scan.sh`)

Comprehensive security scan including authenticated endpoints:
- **Duration**: ~10-20 minutes
- **Coverage**: All endpoints (including authenticated)
- **Features**: AJAX spider, full active scan

**Run:**
```bash
chmod +x testing/security/owasp-zap-full-scan.sh
API_URL=http://localhost:5000 JWT_TOKEN=your-jwt-token ./testing/security/owasp-zap-full-scan.sh
```

### 3. Penetration Testing (`penetration-test.sh`)

Manual penetration tests for common vulnerabilities:
- SQL Injection
- XSS (Cross-Site Scripting)
- Authentication Bypass
- CSRF (Cross-Site Request Forgery)
- Rate Limiting
- Input Validation
- Path Traversal
- Security Headers

**Run:**
```bash
chmod +x testing/security/penetration-test.sh
API_URL=http://localhost:5000 ./testing/security/penetration-test.sh
```

## Running Tests

### Local Development

```bash
# 1. Start Payment API
dotnet run --project src/Payment.API

# 2. Start ZAP (in another terminal)
docker-compose -f testing/security/docker-compose.zap.yml up -d

# 3. Run baseline scan
./testing/security/owasp-zap-baseline.sh

# 4. Run penetration tests
./testing/security/penetration-test.sh
```

### Docker Compose

```bash
# Start all services including ZAP
docker-compose up -d
docker-compose -f testing/security/docker-compose.zap.yml up -d

# Run scans
API_URL=http://payment-api:8080 ./testing/security/owasp-zap-baseline.sh
```

### Kubernetes

```bash
# Port forward to service
kubectl port-forward svc/payment-api 5000:8080

# Start ZAP locally
docker-compose -f testing/security/docker-compose.zap.yml up -d

# Run scans
API_URL=http://localhost:5000 ./testing/security/owasp-zap-baseline.sh
```

## Test Results

Reports are saved to `testing/security/reports/`:
- `zap-report-*.html` - HTML report (human-readable)
- `zap-alerts-*.json` - JSON alerts (machine-readable)
- `zap-report-*.xml` - XML report (CI/CD integration)

### Interpreting Results

**Risk Levels:**
- **High**: Critical vulnerabilities requiring immediate attention
- **Medium**: Important vulnerabilities to address soon
- **Low**: Minor issues or informational findings
- **Informational**: Best practices and recommendations

**Common Findings:**
- Missing security headers (X-Content-Type-Options, X-Frame-Options)
- Missing HTTPS enforcement
- Information disclosure (server version, stack traces)
- Weak authentication mechanisms
- Insecure direct object references

## OWASP Top 10 Coverage

The security tests cover the OWASP Top 10 (2021):

1. ✅ **A01:2021 – Broken Access Control** - Authentication bypass tests
2. ✅ **A02:2021 – Cryptographic Failures** - HTTPS enforcement, encryption
3. ✅ **A03:2021 – Injection** - SQL injection, XSS tests
4. ✅ **A04:2021 – Insecure Design** - Input validation, business logic
5. ✅ **A05:2021 – Security Misconfiguration** - Security headers, error handling
6. ✅ **A06:2021 – Vulnerable Components** - Dependency scanning
7. ✅ **A07:2021 – Authentication Failures** - JWT validation, session management
8. ✅ **A08:2021 – Software and Data Integrity** - Webhook signature validation
9. ✅ **A09:2021 – Security Logging Failures** - Audit logging verification
10. ✅ **A10:2021 – Server-Side Request Forgery** - SSRF tests

## Continuous Integration

Add to CI/CD pipeline:

```yaml
# .github/workflows/security-test.yml
name: Security Tests
on:
  schedule:
    - cron: '0 3 * * 0'  # Weekly on Sunday at 3 AM
  workflow_dispatch:

jobs:
  security:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Start services
        run: docker-compose up -d
      
      - name: Start ZAP
        run: docker-compose -f testing/security/docker-compose.zap.yml up -d
      
      - name: Wait for services
        run: |
          timeout 60 bash -c 'until curl -f http://localhost:5000/health; do sleep 2; done'
          sleep 10  # Wait for ZAP
      
      - name: Run baseline scan
        run: |
          chmod +x testing/security/owasp-zap-baseline.sh
          API_URL=http://localhost:5000 ./testing/security/owasp-zap-baseline.sh
        continue-on-error: true
      
      - name: Run penetration tests
        run: |
          chmod +x testing/security/penetration-test.sh
          API_URL=http://localhost:5000 ./testing/security/penetration-test.sh
      
      - name: Upload reports
        uses: actions/upload-artifact@v3
        if: always()
        with:
          name: security-reports
          path: testing/security/reports/
```

## Remediation

### High-Risk Findings

1. **Review the HTML report** for detailed vulnerability information
2. **Check remediation instructions** in `Payment microservice remediation instructions.md`
3. **Fix vulnerabilities** following security best practices
4. **Re-run scans** to verify fixes

### Common Fixes

**Missing Security Headers:**
- Add `RequestSanitizationMiddleware` (already implemented)
- Configure headers in `Program.cs`

**SQL Injection:**
- Use parameterized queries (EF Core does this automatically)
- Validate all inputs

**XSS:**
- Sanitize user inputs
- Use output encoding
- Implement CSP headers

**Authentication:**
- Validate JWT tokens properly
- Implement rate limiting
- Use strong secrets

## Additional Resources

- [OWASP ZAP Documentation](https://www.zaproxy.org/docs/)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [OWASP Testing Guide](https://owasp.org/www-project-web-security-testing-guide/)
- [Security Policy](../docs/02-Payment/Security_Policy.md)

## Troubleshooting

### ZAP Not Starting
```bash
# Check if port 8080 is available
netstat -an | grep 8080

# Use different port
ZAP_PORT=8090 docker-compose -f testing/security/docker-compose.zap.yml up -d
```

### Scan Timeout
Increase timeout in script:
```bash
TIMEOUT=600 ./testing/security/owasp-zap-baseline.sh
```

### Authentication Issues
Ensure JWT token is valid:
```bash
# Test token
curl -H "Authorization: Bearer ${JWT_TOKEN}" http://localhost:5000/api/v1/payments
```

