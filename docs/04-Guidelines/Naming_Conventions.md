---
title: Naming Conventions
version: 1.0
last_updated: 2025-11-11
category: Guidelines
tags:
  - naming conventions
  - code style
  - best practices
summary: >
  Naming conventions for classes, methods, variables, and other code elements
  following C# and .NET conventions.
related_docs:
  - Coding_Standards.md
  - Testing_Strategy.md
ai_context_priority: medium
---

# 📝 Naming Conventions

## C# Naming Conventions

Follow standard C# naming conventions:

### Classes
- **PascalCase**: `PaymentOrchestrator`, `CreatePaymentCommand`
- **Nouns**: Represent entities or concepts

### Interfaces
- **PascalCase with "I" prefix**: `IPaymentProvider`, `IPaymentRepository`
- **Adjectives or nouns**: Describe capabilities

### Methods
- **PascalCase**: `ProcessPaymentAsync`, `GetPaymentById`
- **Verbs**: Describe actions

### Properties
- **PascalCase**: `PaymentId`, `Amount`, `Status`
- **Nouns**: Represent attributes

### Private Fields
- **camelCase with "_" prefix**: `_logger`, `_unitOfWork`
- **Or camelCase**: `logger`, `unitOfWork` (preferred in modern C#)

### Local Variables
- **camelCase**: `paymentId`, `amount`, `result`

### Constants
- **PascalCase**: `MaxRetryAttempts`, `DefaultTimeout`

### Enums
- **PascalCase**: `PaymentStatus`, `PaymentMethod`
- **Enum values**: `PaymentStatus.Pending`, `PaymentStatus.Completed`

### Namespaces
- **PascalCase**: `Payment.Domain`, `Payment.Application.Handlers`

## File Naming

- **One class per file**: File name matches class name
- **PascalCase**: `PaymentOrchestrator.cs`, `CreatePaymentCommand.cs`

## Project Structure

```
src/
├── Payment.Domain/              # Domain layer
├── Payment.Application/        # Application layer
├── Payment.Infrastructure/     # Infrastructure layer
└── Payment.API/                # Presentation layer

tests/
├── Payment.Domain.Tests/       # Domain tests
├── Payment.Application.Tests/ # Application tests
└── ...
```

## See Also

- [Coding Standards](Coding_Standards.md)
- [Testing Strategy](Testing_Strategy.md)

