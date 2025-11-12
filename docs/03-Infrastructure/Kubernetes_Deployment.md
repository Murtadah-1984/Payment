---
title: Kubernetes Deployment
version: 1.0
last_updated: 2025-11-11
category: Infrastructure
tags:
  - kubernetes
  - deployment
  - docker
  - k8s
  - production
summary: >
  Complete Kubernetes deployment guide including Docker setup, K8s manifests,
  environment variables, and production considerations.
related_docs:
  - Environment_Configuration.md
  - Observability.md
  - Performance_Optimization.md
  - ../00-Overview/README.md
ai_context_priority: high
---

# 📦 Deployment

The service is containerized and ready for Kubernetes deployment with:

## Docker

```bash
docker-compose up -d
```

## Kubernetes

```bash
kubectl apply -f k8s/
```

**Kubernetes Features:**
- ✅ **Health probes** (`/health`, `/ready`) for liveness and readiness checks
- ✅ **Horizontal Pod Autoscaling (HPA)** for automatic scaling
- ✅ **ConfigMaps** for configuration management
- ✅ **Secrets** for sensitive data (JWT keys, DB passwords)
- ✅ **Stateless design** for horizontal scaling
- ✅ **Service** and **Ingress** configurations
- ✅ **Namespace** isolation

## Environment Variables

The application supports three environments: **Development**, **Staging**, and **Production**. Each environment has its own configuration file that automatically loads based on the `ASPNETCORE_ENVIRONMENT` variable.

**Required environment variables for Staging/Production:**

```bash
# Set environment
ASPNETCORE_ENVIRONMENT=Production

# Database
POSTGRES_HOST=postgres-service
POSTGRES_PORT=5432
POSTGRES_DB=PaymentDb
POSTGRES_USER=payment_user
POSTGRES_PASSWORD=<secure-password>

# JWT
AUTH_AUTHORITY=https://identity.yourdomain.com
AUTH_AUDIENCE=payment-service
```

> 📖 **See [Environment Configuration](Environment_Configuration.md) for detailed environment setup guide.**

## Production Considerations

1. **Database**: Use managed PostgreSQL (Azure Database, AWS RDS, etc.)
2. **Secrets**: Use Azure Key Vault, AWS Secrets Manager, or K8s Secrets
3. **Logging**: Integrate with Application Insights, CloudWatch, or ELK Stack
4. **Monitoring**: Add Prometheus metrics and Grafana dashboards
5. **Caching**: Configure Redis for distributed caching
6. **Rate Limiting**: Add rate limiting middleware
7. **API Gateway**: Use API Gateway for routing and authentication
8. **SSL/TLS**: Ensure HTTPS is enforced
9. **Backup**: Configure automated database backups
10. **Disaster Recovery**: Set up multi-region deployment

## See Also

- [Environment Configuration](Environment_Configuration.md)
- [Observability & Monitoring](Observability.md)
- [Performance & Optimization](Performance_Optimization.md)
- [Overview](../00-Overview/README.md)

