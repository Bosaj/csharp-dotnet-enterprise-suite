# System Architecture

## 🏛️ Layered Design Pattern
The solution is organized using Clean Architecture principles:

1. **Domain Layer**: Core entity models and domain interfaces.
2. **Application Layer**: Business logic handlers and DTOs.
3. **Infrastructure Layer**: Data persistence, entity framework core, and external services.
4. **Presentation Layer**: Web API & console user interfaces.

---

## 🛠️ Project Structure
```text
CsharpProjects/
├── Application/      # Business logic & commands
├── Domain/           # Enterprise entities
├── Infrastructure/   # Database & external providers
└── WebApi/           # Controllers & API endpoints
tests/
└── UnitTests/        # xUnit testing suites
```
