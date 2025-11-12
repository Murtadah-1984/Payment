# Testing Infrastructure

This directory contains performance and security testing tools and scripts for the Payment Microservice.

## Directory Structure

```
testing/
├── performance/          # Performance testing with k6
│   ├── load-test.js      # Comprehensive load test
│   ├── stress-test.js    # Stress test to find breaking point
│   ├── spike-test.js     # Spike test for sudden traffic
│   └── README.md         # Performance testing documentation
├── security/            # Security testing with OWASP ZAP
│   ├── owasp-zap-baseline.sh    # Quick baseline scan
│   ├── owasp-zap-full-scan.sh   # Comprehensive scan
│   ├── penetration-test.sh       # Manual penetration tests
│   ├── docker-compose.zap.yml    # ZAP Docker configuration
│   └── README.md                 # Security testing documentation
└── docker-compose.testing.yml    # Testing tools Docker Compose
```

## Quick Start

### Performance Testing

```bash
# Install k6 (see performance/README.md)
# Run load test
k6 run testing/performance/load-test.js
```

### Security Testing

```bash
# Start ZAP
docker-compose -f testing/security/docker-compose.zap.yml up -d

# Run baseline scan
chmod +x testing/security/owasp-zap-baseline.sh
./testing/security/owasp-zap-baseline.sh
```

## Documentation

- [Performance Testing Guide](performance/README.md) - Load, stress, and spike testing
- [Security Testing Guide](security/README.md) - OWASP ZAP scans and penetration testing

## CI/CD Integration

Both performance and security tests can be integrated into CI/CD pipelines:

```yaml
# Example GitHub Actions workflow
name: Testing
on: [push, pull_request]

jobs:
  performance:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Install k6
        run: # ... install k6
      - name: Run load test
        run: k6 run testing/performance/load-test.js
  
  security:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Start ZAP
        run: docker-compose -f testing/security/docker-compose.zap.yml up -d
      - name: Run security scan
        run: ./testing/security/owasp-zap-baseline.sh
```

## Requirements

### Performance Testing
- [k6](https://k6.io/) - Modern load testing tool

### Security Testing
- [OWASP ZAP](https://www.zaproxy.org/) - Security testing tool
- Docker (for running ZAP)
- `curl`, `jq`, `bash` (for scripts)

## Test Results

- **Performance**: Results displayed in console, HTML reports generated
- **Security**: Reports saved to `testing/security/reports/`

## Related Documentation

- [Payment Microservice Remediation Instructions](../Payment%20microservice%20remediation%20instructions.md)
- [Testing Strategy](../docs/04-Guidelines/Testing_Strategy.md)
- [Security Policy](../docs/02-Payment/Security_Policy.md)
- [Performance Optimization](../docs/03-Infrastructure/Performance_Optimization.md)

