# Maliev ServiceDefaults Library

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/MALIEV-Co-Ltd/Maliev.Aspire)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![NuGet](https://img.shields.io/badge/NuGet-Internal-blue)](https://nuget.pkg.dev/MALIEV/Maliev.Aspire.ServiceDefaults)

A comprehensive library of standardized infrastructure patterns and configurations for all MALIEV microservices. This package provides battle-tested, production-ready extension methods that eliminate boilerplate and ensure consistency across the entire platform.

**Role in MALIEV Architecture**: The foundational chassis for all microservices. It abstracts infrastructure complexity (Observability, Auth, DB, Messaging) into a single, unified development experience, enforcing "security by default" and architectural compliance.

---

## 🏗️ Architecture & Tech Stack

- **Core**: .NET 10.0 / C# 13
- **Observability**: OpenTelemetry (Logs, Metrics, Traces)
- **Infrastructure**:
    - **Database**: PostgreSQL with Entity Framework Core
    - **Caching**: Redis Distributed Cache
    - **Messaging**: RabbitMQ via MassTransit
- **Security**: JWT (RSA/HMAC), IAM Integration, Secret Manager
- **API**: OpenAPI 3.1, Scalar UI, API Versioning

---

## ⚖️ Constitution Rules

This library strictly adheres to the platform development mandates:

### Banned Libraries
To maintain high performance and low complexity, the following are **NOT** used:
- ❌ **AutoMapper**: Explicit manual mapping only.
- ❌ **FluentValidation**: Standard Data Annotations or custom validation logic only.
- ❌ **FluentAssertions**: Standard xUnit `Assert` methods only.

### Mandatory Practices
- ✅ **Infrastructure Isolation**: All testing uses **Testcontainers** with real instances (PostgreSQL, Redis, RabbitMQ).
- ✅ **Explicit Configuration**: No hidden magic; all features enabled via explicit extension methods.
- ✅ **Observability Baseline**: Every service must expose standard `/liveness`, `/readiness`, and `/metrics` endpoints.
- ✅ **Secret Protection**: Integrates with Google Secret Manager and `sharedsecrets.json` patterns.

---

## ✨ Key Features

- **Zero-Config Observability**: One line of code for full OpenTelemetry instrumentation.
- **Resilient Infrastructure**: Pre-configured retry policies and circuit breakers for all dependencies.
- **Unified Auth Flow**: Seamless JWT and IAM service integration.
- **Contract-First API**: Built-in versioning and interactive documentation via Scalar.
- **Rapid Service Bootstrapping**: Saves 2-4 hours of boilerplate setup per new service.

---

## 🚀 Quick Start

### Installation

```bash
dotnet add package Maliev.Aspire.ServiceDefaults
```

### Basic Usage

```csharp
using Maliev.Aspire.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add all standard service defaults in one line
builder.AddServiceDefaults();

var app = builder.Build();

// Map standard endpoints (health checks, metrics)
app.MapDefaultEndpoints();

app.Run();
```

---

## 📡 Core Extensions

| Feature | Extension Method | Description |
|---------|------------------|-------------|
| **Observability** | `AddServiceDefaults()` | Configures OTel logging, metrics, and tracing. |
| **Authentication** | `AddJwtAuthentication()` | Configures RSA-2048/HMAC JWT validation. |
| **Database** | `AddPostgresDbContext<T>()` | Pre-configured EF Core with resilience. |
| **Caching** | `AddRedisDistributedCache()` | Redis setup with health checks and retries. |
| **Messaging** | `AddMassTransitWithRabbitMq()` | MassTransit configuration with standard bus setup. |
| **IAM** | `AddIAMServiceClient()` | Resilient client for IAM permission checks. |
| **Versioning** | `AddDefaultApiVersioning()` | Standardized URL-based API versioning. |

---

## 🏥 Health & Monitoring

Standardized health probes for Kubernetes orchestration:
- **Liveness**: `GET /[prefix]/liveness` (Returns 200)
- **Readiness**: `GET /[prefix]/readiness` (Checks DB, Redis, RabbitMQ)
- **Metrics**: `GET /[prefix]/metrics` (Prometheus format)

---

## 🧪 Testing

The library provides `TestcontainersIntegrationTestFactory<T>` to simplify integration testing with real infrastructure:

```csharp
public class MyServiceTests : TestcontainersIntegrationTestFactory<Program>
{
    [Fact]
    public async Task Get_ReturnsSuccess()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/v1/data");
        response.EnsureSuccessStatusCode();
    }
}
```

---

## 📦 Managing Updates

When updating ServiceDefaults, ensuring your service consumes the latest version:
1. Update NuGet dependency version.
2. Review migration guides for breaking changes (if any).
3. Run integration tests to verify infrastructure compatibility.

---

## 📄 License

Proprietary - © 2026 MALIEV Co., Ltd. All rights reserved.
