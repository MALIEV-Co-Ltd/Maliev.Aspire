# Maliev Aspire Orchestrator

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/MALIEV-Co-Ltd/Maliev.Aspire)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Aspire](https://img.shields.io/badge/Aspire-13.1.0-blue)](https://learn.microsoft.com/en-us/dotnet/aspire/)

Automated local development orchestration for the MALIEV microservices platform. This project leverage .NET Aspire to provide a seamless "one-click" experience for running all services and their required infrastructure.

**Role in MALIEV Architecture**: The central orchestrator for local development and integration testing. It manages service dependencies, environment configurations, and provides a unified dashboard for observability and health monitoring.

---

## 🏗️ Architecture & Tech Stack

- **Framework**: .NET Aspire 13.1.0 (.NET 10.0)
- **Infrastructure**: Docker Desktop / Container Runtime
- **Orchestrated Components**:
    - **Microservices**: All MALIEV platform services (Auth, Chatbot, Geometry, etc.)
    - **Database**: PostgreSQL 18
    - **Messaging**: RabbitMQ
    - **Caching**: Redis 7.x
- **Observability**: Aspire Dashboard, OpenTelemetry (Logging, Tracing, Metrics)

---

## ⚖️ Constitution Rules

This service strictly adheres to the platform development mandates:

### Banned Libraries
To maintain high performance and low complexity, the following are **NOT** used:
- ❌ **AutoMapper**: Explicit manual configuration in the AppHost.
- ❌ **FluentValidation**: Environment variable validation via standard .NET patterns.

### Mandatory Practices
- ✅ **TreatWarningsAsErrors**: Enabled in all `.csproj` files.
- ✅ **Shared Infrastructure**: Uses central PostgreSQL, RabbitMQ, and Redis instances.
- ✅ **Secure Configuration**: Uses `sharedsecrets.json` for sensitive credentials (git-ignored).
- ✅ **JWT Key Separation**: Services validate RS256 tokens outside dev/test; `Jwt:SecurityKey` is not a production verifier fallback.

---

## ✨ Key Features

- **Single Command Launch**: Start the entire ecosystem with `dotnet run`.
- **Integrated Dashboard**: Real-time monitoring of service health, logs, and telemetry.
- **Resource Management**: Automatically handles container lifecycle for infrastructure.
- **Environment Parity**: Mimics production service interactions locally.
- **Service Discovery**: Built-in resolution for cross-service communication.

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop or compatible container runtime
- Maliev monorepo cloned locally

### Local Development Setup

1. **Configure Shared Secrets**
   Navigate to the `Maliev.Aspire.AppHost/` directory and create `sharedsecrets.json`.
   ```json
   {
     "ConnectionStrings": {
       "postgres": "Host=localhost;Port=5432;Username=user;Password=password",
       "rabbitmq": "amqp://guest:guest@localhost:5672",
       "redis": "localhost:6379"
     }
   }
   ```
   > [!TIP]
   > Use `sharedsecrets.json.template` as a starting point.
   >
   > Production-like runs must provide `Jwt:PublicKey` and `Jwt:PrivateKey` as Base64-encoded RSA PEM values.
   > `Jwt:SecurityKey` is only a Development/Testing compatibility fallback.

2. **Run the Orchestrator**
   ```bash
   cd Maliev.Aspire.AppHost
   dotnet run
   ```

3. **Explore the Dashboard**
   Open the Aspire Dashboard URL displayed in your console (typically `http://localhost:17006`) to monitor your services.

---

## 🏥 Health & Monitoring

The Aspire Dashboard provides comprehensive visibility:
- **Resources**: Real-time status of all microservices and infrastructure.
- **Console Logs**: Live output from all orchestrated processes.
- **Traces**: End-to-end request tracing across service boundaries.
- **Metrics**: Resource utilization and custom telemetry.

---

## 🧪 Testing

```bash
# Run orchestration tests
dotnet test Maliev.Aspire.Tests
```

- **AppHost Tests**: Verifies that the orchestration graph is built correctly.
- **Integration Tests**: Validates cross-service connectivity within the Aspire environment.

---

## 📦 Managing Services

To modify the orchestration:
1. **Add/Remove References**: Edit `Maliev.Aspire.AppHost.csproj`.
2. **Update Topology**: Add or remove `builder.AddProject<T>()` in `AppHost.cs`.

---

## 📄 License

Proprietary - © 2026 MALIEV Co., Ltd. All rights reserved.
