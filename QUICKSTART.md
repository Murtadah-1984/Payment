# Quick Start Guide

## Prerequisites

- .NET 8 SDK
- Docker Desktop (optional, for containerized deployment)
- PostgreSQL (or use Docker Compose)

## Local Development

### 1. Setup Database

Using Docker Compose:
```bash
docker-compose up -d postgres
```

Or use your own PostgreSQL instance and update the connection string in `appsettings.Development.json`.

### 2. Run Database Migrations

```bash
cd src/Payment.API
dotnet ef migrations add InitialCreate --project ../Payment.Infrastructure
dotnet ef database update --project ../Payment.Infrastructure
```

### 3. Run the API

```bash
dotnet run --project src/Payment.API
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `https://localhost:5001/swagger`

## Docker Deployment

### Build and Run

```bash
docker-compose up -d
```

This will:
- Start PostgreSQL database
- Build and start the Payment API
- Run database migrations automatically

## Kubernetes Deployment

### 1. Build Docker Image

```bash
docker build -t payment-api:latest .
```

### 2. Apply Kubernetes Manifests

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml  # Update secrets first!
kubectl apply -f k8s/postgres-deployment.yaml
kubectl apply -f k8s/payment-deployment.yaml
kubectl apply -f k8s/hpa.yaml
```

### 3. Update Secrets

**Important**: Before deploying, update the secrets in `k8s/secret.yaml` and `k8s/postgres-deployment.yaml` with secure values.

## Testing

Run all tests:
```bash
dotnet test
```

## API Authentication

The API uses JWT Bearer Token authentication. To test:

1. Obtain a JWT token from your authentication service
2. Include it in the Authorization header: `Bearer <your-token>`
3. Or temporarily disable authentication in `Program.cs` for development

## Health Checks

- `/health` - Basic health check
- `/ready` - Readiness probe (includes database check)

## Environment Variables

Key environment variables:
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `JwtSettings__SecretKey` - JWT signing key (min 32 characters)
- `JwtSettings__Issuer` - JWT issuer
- `JwtSettings__Audience` - JWT audience

