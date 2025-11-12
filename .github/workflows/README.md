# GitHub Actions Workflows

This directory contains comprehensive CI/CD workflows for the Payment API microservice.

## Workflows Overview

### 🔍 CI - Build, Test & Quality (`ci.yml`)
Runs on every push and pull request to `main` and `develop` branches.

**Jobs:**
- **Code Quality**: Checks code formatting and runs analyzers
- **Security Scan**: Scans for vulnerabilities in dependencies
- **Build**: Builds the entire solution in Release configuration
- **Test**: Runs all unit tests with code coverage across all test projects
- **Coverage**: Generates and uploads code coverage reports
- **Docker Build Test**: Validates Docker image can be built successfully

**Artifacts:**
- Build artifacts
- Test results
- Code coverage reports

### 🚀 CD - Build & Deploy (`cd.yml`)
Runs on pushes to `main`/`develop` branches, tags, and manual workflow dispatch.

**Features:**
- Automatic environment detection (development/staging/production)
- Docker image building with multi-tag support
- Container registry push to GitHub Container Registry
- Security scanning with Trivy
- Kubernetes deployment automation
- Production deployments require manual approval
- Automatic GitHub releases for version tags

**Environments:**
- `development`: Auto-deploys from `develop` branch
- `staging`: Auto-deploys from `main` branch
- `production`: Requires manual approval, deploys from version tags

### 🔒 Dependency Review (`dependency-review.yml`)
Automatically reviews dependencies in pull requests for security vulnerabilities and license compliance.

### 🛡️ CodeQL Analysis (`codeql-analysis.yml`)
Advanced static analysis for security vulnerabilities and code quality issues. Runs on:
- Every push to `main`/`develop`
- Pull requests
- Weekly schedule (Sunday)
- Manual trigger

## Setup Requirements

### GitHub Secrets
For production deployments, configure these secrets in your repository:

- `AKS_RESOURCE_GROUP`: Azure Resource Group (if using Azure AKS)
- `AKS_CLUSTER_NAME`: Azure Kubernetes Service cluster name
- Or configure kubectl for your specific Kubernetes provider

### GitHub Environments
Configure environments in GitHub repository settings:
1. Go to Settings → Environments
2. Create environments: `development`, `staging`, `production`
3. For `production`, enable "Required reviewers" for manual approval

### Container Registry
The workflow uses GitHub Container Registry (`ghcr.io`) by default. The `GITHUB_TOKEN` is automatically used for authentication.

## Usage

### Automatic CI/CD
- Push to `develop` → Runs CI → Auto-deploys to development
- Push to `main` → Runs CI → Auto-deploys to staging
- Create version tag (`v1.0.0`) → Runs CI → Deploys to production (with approval)

### Manual Deployment
1. Go to Actions tab
2. Select "CD - Build & Deploy" workflow
3. Click "Run workflow"
4. Select environment and optional version
5. Click "Run workflow"

### Viewing Results
- **CI Results**: Check the Actions tab for build/test status
- **Coverage Reports**: Download from workflow artifacts
- **Security Findings**: View in Security → Code scanning alerts
- **Deployment Status**: Check environment deployment logs

## Workflow Features

✅ **Parallel Execution**: Jobs run in parallel where possible for faster feedback  
✅ **Caching**: NuGet packages and Docker layers are cached  
✅ **Matrix Testing**: Tests run in parallel across all test projects  
✅ **Security First**: Multiple security scanning layers  
✅ **Kubernetes Ready**: Automated K8s deployment with health checks  
✅ **Production Safe**: Manual approval gates for production  
✅ **Comprehensive Reporting**: Detailed summaries and artifacts  

## Customization

### Adjust Test Coverage Thresholds
Edit `ci.yml` to add coverage thresholds:
```yaml
- name: Run tests with coverage
  run: |
    dotnet test ... -- /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80
```

### Add Integration Tests
Create a new job in `ci.yml`:
```yaml
integration-tests:
  name: Integration Tests
  runs-on: ubuntu-latest
  services:
    postgres:
      image: postgres:15-alpine
      env:
        POSTGRES_PASSWORD: postgres
      options: >-
        --health-cmd pg_isready
        --health-interval 10s
        --health-timeout 5s
        --health-retries 5
```

### Custom Kubernetes Provider
Update the `configure kubectl` step in `cd.yml` with your provider's authentication method.

## Best Practices

1. **Never skip CI**: All code must pass CI before merging
2. **Review security alerts**: Address moderate+ severity issues promptly
3. **Monitor deployments**: Check deployment logs and health endpoints
4. **Version tagging**: Use semantic versioning (`v1.0.0`) for releases
5. **Environment parity**: Keep dev/staging/prod configurations similar

