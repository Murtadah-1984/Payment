---
title: Contributing Guide
version: 1.0
last_updated: 2025-11-11
category: Guidelines
tags:
  - contributing
  - development
  - pull requests
summary: >
  Guide for contributing to the Payment Microservice, including development
  setup, coding standards, and pull request process.
related_docs:
  - Coding_Standards.md
  - Testing_Strategy.md
ai_context_priority: medium
---

# 🤝 Contributing

## Development Setup

1. **Clone the repository**
2. **Install .NET 8 SDK**
3. **Install PostgreSQL** (or use Docker)
4. **Run the application**: `dotnet run --project src/Payment.API`
5. **Run tests**: `dotnet test`

## Coding Standards

- Follow [Coding Standards](Coding_Standards.md)
- Follow [Naming Conventions](Naming_Conventions.md)
- Write tests for all new features
- Update documentation

## Pull Request Process

1. **Create a feature branch**: `git checkout -b feature/new-feature`
2. **Make changes**: Follow coding standards and write tests
3. **Run tests**: `dotnet test`
4. **Update documentation**: Update relevant docs
5. **Create PR**: Provide clear description of changes
6. **Code review**: Address review comments
7. **Merge**: After approval, merge to main

## Commit Messages

Use clear, descriptive commit messages:

```
feat: Add new payment provider integration
fix: Fix webhook signature validation bug
docs: Update API documentation
test: Add unit tests for PaymentOrchestrator
refactor: Extract payment processing logic
```

## See Also

- [Coding Standards](Coding_Standards.md)
- [Testing Strategy](Testing_Strategy.md)
- [Extension Guide](Extension_Guide.md)

