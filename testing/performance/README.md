# Performance Testing

This directory contains performance testing scripts using [k6](https://k6.io/) for load testing the Payment Microservice.

## Prerequisites

Install k6:
```bash
# Windows (using Chocolatey)
choco install k6

# macOS (using Homebrew)
brew install k6

# Linux
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6

# Docker
docker pull grafana/k6
```

## Test Scripts

### 1. Load Test (`load-test.js`)
Comprehensive load test with gradual ramp-up and ramp-down:
- **Stages**: 10 → 50 → 100 → 200 → 50 → 0 users
- **Duration**: ~10 minutes
- **Thresholds**:
  - 95% of requests < 2s
  - 99% of requests < 5s
  - Error rate < 1%
  - Payment creation: 95% < 3s
  - Payment query: 95% < 500ms

**Run:**
```bash
k6 run --vus 100 --duration 5m testing/performance/load-test.js
```

**With environment variables:**
```bash
BASE_URL=http://localhost:5000 JWT_TOKEN=your-token k6 run testing/performance/load-test.js
```

### 2. Stress Test (`stress-test.js`)
Gradually increases load to find breaking point:
- **Stages**: 50 → 100 → 200 → 300 → 400 → 500 users
- **Duration**: ~12 minutes
- **Purpose**: Identify maximum capacity and failure points

**Run:**
```bash
k6 run testing/performance/stress-test.js
```

### 3. Spike Test (`spike-test.js`)
Tests system resilience to sudden traffic spikes:
- **Stages**: 100 → 1000 → 100 users (sudden changes)
- **Duration**: ~3 minutes
- **Purpose**: Test rate limiting and circuit breaker behavior

**Run:**
```bash
k6 run testing/performance/spike-test.js
```

## Environment Variables

- `BASE_URL`: API base URL (default: `http://localhost:5000`)
- `API_VERSION`: API version (default: `v1`)
- `JWT_TOKEN`: JWT bearer token for authenticated requests

## Running Tests

### Local Development
```bash
# Start the Payment API
dotnet run --project src/Payment.API

# In another terminal, run load test
k6 run testing/performance/load-test.js
```

### Docker Compose
```bash
# Start services
docker-compose up -d

# Run load test
k6 run --env BASE_URL=http://localhost:5000 testing/performance/load-test.js
```

### Kubernetes
```bash
# Port forward to service
kubectl port-forward svc/payment-api 5000:8080

# Run load test
k6 run --env BASE_URL=http://localhost:5000 testing/performance/load-test.js
```

## Performance Benchmarks

### Target Metrics (from remediation instructions)

**Business Metrics:**
- Payment success rate: >99%
- Average payment processing time: <2s
- Provider availability: >99.9%

**Technical Metrics:**
- API response time:
  - p50: <500ms
  - p95: <2000ms
  - p99: <5000ms
- Database query time: <100ms (p95)
- Cache hit rate: >80%
- Error rate: <0.1%

### Interpreting Results

k6 generates detailed metrics:
- `http_req_duration`: Request duration distribution
- `http_req_failed`: Failed request rate
- `payment_creation_time`: Custom metric for payment creation
- `payment_query_time`: Custom metric for payment queries

**Example output:**
```
✓ payment creation status is 201
✓ payment creation has payment ID
✓ payment query status is 200
✓ payment query has payment data

checks.........................: 100.00% ✓ 5000    ✗ 0
data_received..................: 2.5 MB  42 kB/s
data_sent......................: 1.2 MB  20 kB/s
http_req_duration..............: avg=450ms  min=120ms  med=380ms  max=2.1s  p(95)=1.2s  p(99)=2.8s
http_req_failed................: 0.00%   ✓ 0       ✗ 5000
payment_creation_time..........: avg=420ms  min=100ms  med=350ms  max=2.0s  p(95)=1.1s  p(99)=2.5s
payment_query_time.............: avg=85ms   min=25ms   med=70ms   max=450ms  p(95)=180ms  p(99)=320ms
```

## Continuous Integration

Add to CI/CD pipeline:
```yaml
# .github/workflows/performance-test.yml
name: Performance Tests
on:
  schedule:
    - cron: '0 2 * * *'  # Daily at 2 AM
  workflow_dispatch:

jobs:
  performance:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Install k6
        run: |
          sudo gpg -k
          sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
          echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
          sudo apt-get update
          sudo apt-get install k6
      - name: Start services
        run: docker-compose up -d
      - name: Wait for API
        run: |
          timeout 60 bash -c 'until curl -f http://localhost:5000/health; do sleep 2; done'
      - name: Run load test
        run: k6 run testing/performance/load-test.js
      - name: Upload results
        uses: actions/upload-artifact@v3
        if: always()
        with:
          name: k6-results
          path: summary.html
```

## Troubleshooting

### High Error Rates
- Check database connection pool size
- Verify Redis cache is running
- Review application logs for exceptions
- Check rate limiting configuration

### Slow Response Times
- Enable query logging in EF Core
- Check database indexes
- Review cache hit rates
- Monitor external provider response times

### Rate Limiting Issues
- Adjust `IpRateLimiting` configuration in `appsettings.json`
- Consider distributed rate limiting with Redis
- Review spike test results for rate limit behavior

## Additional Resources

- [k6 Documentation](https://k6.io/docs/)
- [k6 Metrics](https://k6.io/docs/using-k6/metrics/)
- [Performance Testing Best Practices](https://k6.io/docs/test-types/)

