# Maliev.Aspire.ServiceDefaults

A comprehensive library of standardized infrastructure patterns and configurations for all Maliev microservices. This package provides battle-tested, production-ready extension methods that eliminate boilerplate code and ensure consistency across the entire platform.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Core Features](#core-features)
  - [Service Discovery & Observability](#service-discovery--observability)
  - [Authentication](#authentication)
  - [Database](#database)
  - [Redis Cache](#redis-cache)
  - [Message Queue (MassTransit)](#message-queue-masstransit)
  - [API Documentation](#api-documentation)
  - [API Versioning](#api-versioning)
  - [CORS](#cors)
  - [IAM Integration](#iam-integration)
  - [Secrets Management](#secrets-management)
  - [Testing Utilities](#testing-utilities)
- [Usage Examples](#usage-examples)
- [Configuration Reference](#configuration-reference)
- [Best Practices](#best-practices)

## Overview

ServiceDefaults consolidates common infrastructure patterns used across all 22+ Maliev microservices, including:

- OpenTelemetry integration (logging, metrics, tracing)
- Health checks (liveness & readiness)
- JWT authentication (RSA & HMAC)
- PostgreSQL with Entity Framework Core
- Redis distributed caching with resilience
- RabbitMQ messaging with MassTransit
- API versioning and documentation
- CORS configuration
- IAM service integration
- Google Secret Manager support

**Benefits:**
- Reduces boilerplate by 30-40% per service
- Ensures consistent behavior across all microservices
- Single source of truth for security and infrastructure patterns
- Faster development of new services (saves 2-4 hours per service)
- Centralized updates (security patches propagate to all services)

## Quick Start

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

// Add additional services
builder.Services.AddControllers();

var app = builder.Build();

// Map standard endpoints (health checks, metrics)
app.MapDefaultEndpoints();

// Your custom endpoints
app.MapControllers();

app.Run();
```

That's it! Your service now has:
- OpenTelemetry logging, metrics, and tracing
- Health checks at `/[service-prefix]/liveness` and `/[service-prefix]/readiness`
- Prometheus metrics at `/[service-prefix]/metrics`
- Service discovery integration
- Resilience policies for HTTP clients

## Core Features

### Service Discovery & Observability

#### `AddServiceDefaults()`

Configures OpenTelemetry with logging, metrics, and distributed tracing.

```csharp
builder.AddServiceDefaults();
```

**What it provides:**
- **Logging**: Structured logging with OTLP exporter
- **Metrics**: Prometheus endpoint at `/[prefix]/metrics`
- **Tracing**: Distributed tracing with W3C Trace Context
- **Runtime Instrumentation**: ASP.NET Core, HttpClient, and runtime metrics
- **Health Checks**: Liveness and readiness probes

**Metrics Exposed:**
- HTTP request duration and count
- .NET runtime metrics (GC, thread pool, exceptions)
- Process metrics (CPU, memory)
- Custom business metrics (via `IMeterFactory`)

#### `MapDefaultEndpoints()`

Maps standard health check and metrics endpoints.

```csharp
app.MapDefaultEndpoints();
```

**Endpoints created:**
- `GET /[prefix]/liveness` - Always returns 200 (K8s liveness probe)
- `GET /[prefix]/readiness` - Returns 200 if all dependencies are healthy
- `GET /[prefix]/metrics` - Prometheus-format metrics

**Readiness checks include:**
- Database connectivity
- Redis availability
- RabbitMQ connection
- IAM service health (if configured)

### Authentication

#### `AddJwtAuthentication()`

Configures JWT bearer token authentication with support for both RSA and HMAC signing.

```csharp
// RSA (recommended for production)
builder.AddJwtAuthentication();

// Or with custom configuration
builder.AddJwtAuthentication(options =>
{
    options.ValidateIssuer = true;
    options.ValidIssuer = "Maliev.IAMService";
    options.ValidateAudience = true;
    options.ValidAudiences = new[] { "Maliev.Services" };
});
```

**Configuration (appsettings.json):**
```json
{
  "Jwt": {
    "Key": "base64-encoded-rsa-public-key-or-hmac-secret",
    "SigningAlgorithm": "RS256",  // or "HS256"
    "Issuer": "Maliev.IAMService",
    "Audience": "Maliev.Services",
    "ClockSkew": 300  // seconds (default: 5 minutes)
  }
}
```

**Supported Algorithms:**
- **RS256**: RSA signature with SHA-256 (recommended)
- **HS256**: HMAC signature with SHA-256 (symmetric key)

**Special Handling:**
- Testing environment: Bypasses signature validation for easier testing
- Automatic claim mapping: Standard JWT claims to ASP.NET Core identity
- 5-minute clock skew tolerance for time-based validation

### Database

#### `AddPostgresDbContext<TContext>()`

Configures PostgreSQL with Entity Framework Core, health checks, and migration support.

```csharp
builder.AddPostgresDbContext<EmployeeDbContext>("EmployeeDatabase");
```

**Features:**
- **Connection Retry**: 5 retries with exponential backoff (max 10s delay)
- **Command Timeout**: 30 seconds default
- **Health Check**: Automatic health check for readiness probe
- **Development Features**: Sensitive data logging (dev only), detailed errors
- **Dynamic JSON**: Optional support for JSON columns

**Configuration:**
```json
{
  "ConnectionStrings": {
    "EmployeeDatabase": "Host=postgres;Database=employees;Username=app;Password=secret"
  }
}
```

#### `MigrateDatabaseAsync<TContext>()`

Safely applies Entity Framework migrations at startup.

```csharp
try
{
    await app.MigrateDatabaseAsync<EmployeeDbContext>();
}
catch (Exception ex)
{
    // Logs error but doesn't crash the app (allows debugging)
    logger.LogError(ex, "Database migration failed");
}
```

**Features:**
- Skips migration in "Testing" environment
- Automatic migration application
- Graceful error handling (logs but doesn't crash)
- Suitable for both development and production

**Best Practice:**
```csharp
if (!app.Environment.IsEnvironment("Testing"))
{
    await app.MigrateDatabaseAsync<EmployeeDbContext>();
}
```

### Redis Cache

#### `AddRedisDistributedCache()`

Configures Redis distributed cache with health checks and resilience.

```csharp
builder.AddRedisDistributedCache("Cache");
```

**Features:**
- **Connection Retry**: Exponential backoff (1s to 10s)
- **Timeouts**: 15-second connect/sync timeouts
- **Health Check**: Automatic health check for readiness
- **Connection Multiplexer**: Available via DI for advanced scenarios

**Configuration:**
```json
{
  "ConnectionStrings": {
    "Cache": "redis:6379,password=secret,ssl=true"
  }
}
```

**Usage:**
```csharp
public class MyService
{
    private readonly IDistributedCache _cache;

    public MyService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string?> GetCachedValueAsync(string key)
    {
        return await _cache.GetStringAsync(key);
    }
}
```

### Message Queue (MassTransit)

#### `AddMassTransitWithRabbitMq()`

Configures MassTransit with RabbitMQ, consumers, and health checks.

```csharp
builder.AddMassTransitWithRabbitMq(cfg =>
{
    // Register consumers
    cfg.AddConsumer<OrderCreatedEventConsumer>();
    cfg.AddConsumer<PaymentProcessedEventConsumer>();

    // Custom endpoint configuration
    cfg.AddConfigureEndpoints(new KebabCaseEndpointNameFormatter("employee", false));
});
```

**Features:**
- **Non-blocking startup**: RabbitMQ connection doesn't block app startup
- **Health Check**: Connection health included in readiness probe
- **Heartbeat**: 60-second heartbeat configuration
- **Automatic Retry**: Built-in retry policies
- **Message Serialization**: JSON with camelCase

**Configuration:**
```json
{
  "RabbitMQ": {
    "Host": "rabbitmq",
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

**Publishing Events:**
```csharp
public class OrderService
{
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task CreateOrderAsync(Order order)
    {
        // ... business logic

        await _publishEndpoint.Publish(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount
        });
    }
}
```

### API Documentation

#### `MapApiDocumentation()`

Maps OpenAPI and Scalar UI documentation endpoints (non-production only).

```csharp
app.MapApiDocumentation(servicePrefix: "employees");
```

**Endpoints created:**
- `GET /employees/openapi/v1.json` - OpenAPI spec
- `GET /employees/scalar` - Interactive Scalar UI

**Features:**
- Only enabled in non-production environments
- Service-prefixed routes
- Automatic discovery of API endpoints
- Scalar UI for interactive testing

**Configure OpenAPI metadata:**
```csharp
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "Employee Service API";
        document.Info.Version = "v1";
        document.Info.Description = "Manages employee data and operations";
        return Task.CompletedTask;
    });
});
```

### API Versioning

#### `AddDefaultApiVersioning()`

Configures API versioning with sensible defaults.

```csharp
builder.AddDefaultApiVersioning();
```

**Features:**
- **URL Segment Versioning**: Routes like `/v1/employees`
- **Default Version**: v1.0
- **API Explorer**: Integration with OpenAPI
- **Version Header**: Advertises supported versions in response headers

**Usage in Controllers:**
```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/employees")]
public class EmployeesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetEmployees() { }
}

[ApiController]
[ApiVersion("2.0")]
[Route("v{version:apiVersion}/employees")]
public class EmployeesV2Controller : ControllerBase
{
    [HttpGet]
    public IActionResult GetEmployees() { }
}
```

### CORS

#### `AddDefaultCors()`

Configures CORS with configuration-based allowed origins.

```csharp
builder.AddDefaultCors();
```

**Configuration:**
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.maliev.com",
      "https://admin.maliev.com"
    ]
  }
}
```

**Features:**
- Allows credentials
- Configurable allowed origins
- Default policy support
- All HTTP methods allowed
- All headers allowed

**Usage:**
```csharp
app.UseCors();  // Apply CORS before MapControllers()
app.MapControllers();
```

### IAM Integration

#### `AddIAMServiceClient()`

Configures HTTP client for IAM service integration with resilience.

```csharp
builder.AddIAMServiceClient();
```

**Configuration:**
```json
{
  "ExternalServices": {
    "IAM": {
      "BaseUrl": "http://iam-service:8080",
      "ServiceAccountToken": "your-service-account-token",
      "Timeout": 5000
    }
  }
}
```

**Features:**
- Standard resilience handler (retry, circuit breaker, timeout)
- Service account token authentication
- Health check integration

**Usage:**
```csharp
public class MyService
{
    private readonly HttpClient _iamClient;

    public MyService(IHttpClientFactory factory)
    {
        _iamClient = factory.CreateClient("IAMService");
    }

    public async Task<bool> CheckPermissionAsync(string userId, string permission)
    {
        var response = await _iamClient.PostAsJsonAsync("/api/iam/auth/check-permission", new
        {
            PrincipalId = userId,
            Permission = permission
        });

        return response.IsSuccessStatusCode;
    }
}
```

#### IAM Registration Service (Base Class)

Base class for registering service permissions and roles with IAM.

**Location:** `Maliev.Aspire.ServiceDefaults.IAM.IAMRegistrationService`

```csharp
public class EmployeeIAMRegistrationService : IAMRegistrationService
{
    public EmployeeIAMRegistrationService(
        IHttpClientFactory httpClientFactory,
        ILogger<EmployeeIAMRegistrationService> logger,
        IConfiguration configuration)
        : base(httpClientFactory, logger, configuration)
    {
    }

    protected override string ServiceName => "EmployeeService";

    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return new[]
        {
            new PermissionRegistration
            {
                Id = "employees.read",
                Name = "Read Employees",
                Description = "View employee information"
            },
            new PermissionRegistration
            {
                Id = "employees.write",
                Name = "Write Employees",
                Description = "Create and update employees"
            },
            new PermissionRegistration
            {
                Id = "employees.delete",
                Name = "Delete Employees",
                Description = "Delete employee records"
            }
        };
    }

    protected override IEnumerable<RoleRegistration> GetRoles()
    {
        return new[]
        {
            new RoleRegistration
            {
                Id = "hr_manager",
                Name = "HR Manager",
                Description = "Can manage all employee operations",
                Permissions = new[] { "employees.read", "employees.write", "employees.delete" }
            },
            new RoleRegistration
            {
                Id = "hr_viewer",
                Name = "HR Viewer",
                Description = "Can only view employee information",
                Permissions = new[] { "employees.read" }
            }
        };
    }
}

// Register in Program.cs
builder.Services.AddHostedService<EmployeeIAMRegistrationService>();
```

**Features:**
- Automatic registration on service startup
- Retry logic with exponential backoff
- Logs registration status
- Supports both permissions and roles

### Secrets Management

#### `AddKeyPerFileSecrets()`

Configures Google Secret Manager volume-mounted secrets.

```csharp
builder.AddKeyPerFileSecrets("/secrets");
```

**Features:**
- Reads secrets from directory (one file per secret)
- Standard GCP Secret Manager integration
- Automatic configuration binding

**Directory structure:**
```
/secrets/
  database-password
  jwt-signing-key
  api-key
```

**Usage:**
```csharp
var dbPassword = builder.Configuration["database-password"];
```

### Testing Utilities

#### `TestcontainersIntegrationTestFactory`

Base class for integration tests using Testcontainers.

**Location:** `Maliev.Aspire.ServiceDefaults.Testing.TestcontainersIntegrationTestFactory<TEntryPoint>`

```csharp
public class EmployeeServiceTests : TestcontainersIntegrationTestFactory<Program>
{
    [Fact]
    public async Task GetEmployees_ReturnsSuccess()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/v1/employees");

        response.EnsureSuccessStatusCode();
    }
}
```

**Features:**
- Automatic PostgreSQL container setup
- Automatic Redis container setup
- Automatic RabbitMQ container setup
- Database migration on startup
- Cleanup after tests
- Isolated test environment per test class

## Usage Examples

### Minimal Service Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add all defaults
builder.AddServiceDefaults();

// Add database
builder.AddPostgresDbContext<MyDbContext>("MyDatabase");

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();

// Apply migrations
await app.MigrateDatabaseAsync<MyDbContext>();

// Map endpoints
app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
```

### Full-Featured Service

```csharp
var builder = WebApplication.CreateBuilder(args);

// Service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Authentication
builder.AddJwtAuthentication();

// Database
builder.AddPostgresDbContext<OrderDbContext>("OrderDatabase", enableDynamicJson: true);

// Cache
builder.AddRedisDistributedCache("Cache");

// Messaging
builder.AddMassTransitWithRabbitMq(cfg =>
{
    cfg.AddConsumer<PaymentProcessedConsumer>();
    cfg.AddConsumer<InventoryReservedConsumer>();
});

// API versioning
builder.AddDefaultApiVersioning();

// CORS
builder.AddDefaultCors();

// IAM integration
builder.AddIAMServiceClient();

// Controllers
builder.Services.AddControllers();

// Custom services
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// Migrations
await app.MigrateDatabaseAsync<OrderDbContext>();

// Middleware pipeline
app.UseAuthentication();
app.UseAuthorization();
app.UseCors();

// Endpoints
app.MapDefaultEndpoints();
app.MapApiDocumentation("orders");
app.MapControllers();

app.Run();
```

### Integration Test Setup

```csharp
public class OrderServiceIntegrationTests : TestcontainersIntegrationTestFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services with mocks if needed
            services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        });
    }

    [Fact]
    public async Task CreateOrder_ValidData_ReturnsCreated()
    {
        var client = CreateClient();

        var order = new { CustomerId = "C123", Items = new[] { new { ProductId = "P1", Quantity = 2 } } };
        var response = await client.PostAsJsonAsync("/v1/orders", order);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
```

## Configuration Reference

### appsettings.json Template

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Database": "Host=postgres;Database=mydb;Username=app;Password=secret",
    "Cache": "redis:6379,password=secret"
  },
  "RabbitMQ": {
    "Host": "rabbitmq",
    "Username": "guest",
    "Password": "guest"
  },
  "Jwt": {
    "Key": "base64-encoded-key",
    "SigningAlgorithm": "RS256",
    "Issuer": "Maliev.IAMService",
    "Audience": "Maliev.Services"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://app.maliev.com"
    ]
  },
  "ExternalServices": {
    "IAM": {
      "BaseUrl": "http://iam-service:8080",
      "ServiceAccountToken": "token",
      "Timeout": 5000
    }
  }
}
```

## Best Practices

### Service Startup Order

Always configure services in this order:

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. ServiceDefaults first (establishes observability baseline)
builder.AddServiceDefaults();

// 2. Authentication/Authorization
builder.AddJwtAuthentication();
builder.Services.AddAuthorization();

// 3. Infrastructure (database, cache, messaging)
builder.AddPostgresDbContext<MyDbContext>("Database");
builder.AddRedisDistributedCache("Cache");
builder.AddMassTransitWithRabbitMq(cfg => { });

// 4. External service clients
builder.AddIAMServiceClient();

// 5. API features (versioning, CORS, documentation)
builder.AddDefaultApiVersioning();
builder.AddDefaultCors();

// 6. MVC/Controllers
builder.Services.AddControllers();

// 7. Business logic services
builder.Services.AddScoped<IMyService, MyService>();

var app = builder.Build();

// 8. Run migrations
await app.MigrateDatabaseAsync<MyDbContext>();

// 9. Middleware pipeline (order matters!)
app.UseAuthentication();
app.UseAuthorization();
app.UseCors();

// 10. Map endpoints
app.MapDefaultEndpoints();
app.MapApiDocumentation("my-service");
app.MapControllers();

app.Run();
```

### Health Check Configuration

Health checks automatically include:
- Database: Connection test
- Redis: Ping test
- RabbitMQ: Connection test
- IAM: Availability check (if configured)

To add custom health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<ExternalApiHealthCheck>("external-api");
```

### Environment-Specific Configuration

```csharp
if (app.Environment.IsDevelopment())
{
    // Development-only features
    app.MapApiDocumentation("my-service");
}

if (app.Environment.IsProduction())
{
    // Production-only features
    app.UseHsts();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    // Skip in test environment
    await app.MigrateDatabaseAsync<MyDbContext>();
}
```

### Secrets Management

Never commit secrets to source control. Use one of these approaches:

1. **User Secrets** (Development):
```bash
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;..."
```

2. **Environment Variables** (Docker/K8s):
```bash
export ConnectionStrings__Database="Host=postgres;..."
```

3. **Google Secret Manager** (GKE):
```csharp
builder.AddKeyPerFileSecrets("/secrets");
```

### Performance Tuning

#### Database Connection Pooling

EF Core pools connections automatically. Configure pool size if needed:

```json
{
  "ConnectionStrings": {
    "Database": "Host=postgres;Database=mydb;Pooling=true;MinPoolSize=5;MaxPoolSize=100"
  }
}
```

#### Redis Connection Multiplexer

Redis connections are pooled automatically. For advanced scenarios:

```csharp
public class MyService
{
    private readonly IConnectionMultiplexer _redis;

    public MyService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<string?> GetAsync(string key)
    {
        var db = _redis.GetDatabase();
        return await db.StringGetAsync(key);
    }
}
```

#### HTTP Client Resilience

All HTTP clients use `AddStandardResilienceHandler()` which includes:
- **Retry**: 3 retries with exponential backoff
- **Circuit Breaker**: Opens after 5 consecutive failures
- **Timeout**: 30 seconds per request

Customize if needed:
```csharp
builder.Services.AddHttpClient("MyClient")
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 5;
        options.Timeout.Timeout = TimeSpan.FromSeconds(60);
    });
```

## Migration Guide

### Migrating Existing Services

If you have an existing service, follow these steps:

1. **Install ServiceDefaults:**
```bash
dotnet add package Maliev.Aspire.ServiceDefaults
```

2. **Replace manual OpenTelemetry setup:**
```csharp
// Before
builder.Logging.AddOpenTelemetry(...);
builder.Services.AddOpenTelemetry().WithMetrics(...).WithTracing(...);

// After
builder.AddServiceDefaults();
```

3. **Replace database configuration:**
```csharp
// Before
builder.Services.AddDbContext<MyDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database"));
    // ... 10+ lines of configuration
});

// After
builder.AddPostgresDbContext<MyDbContext>("Database");
```

4. **Replace Redis configuration:**
```csharp
// Before
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Cache");
    // ... configuration
});

// After
builder.AddRedisDistributedCache("Cache");
```

5. **Replace MassTransit configuration:**
```csharp
// Before
builder.Services.AddMassTransit(x =>
{
    x.AddConsumers(typeof(Program).Assembly);
    x.UsingRabbitMq((context, cfg) =>
    {
        // ... 20+ lines
    });
});

// After
builder.AddMassTransitWithRabbitMq(cfg =>
{
    cfg.AddConsumer<MyConsumer>();
});
```

## Troubleshooting

### Common Issues

**Database migration fails:**
- Ensure connection string is correct
- Check database server is accessible
- Verify credentials have migration permissions
- Check for pending migrations: `dotnet ef migrations list`

**Redis connection fails:**
- Verify Redis is running: `redis-cli ping`
- Check connection string format
- Ensure firewall allows connection
- Check for authentication requirements

**RabbitMQ connection fails:**
- Verify RabbitMQ is running: `rabbitmqctl status`
- Check credentials are correct
- Ensure virtual host exists
- Check firewall/network access

**Health checks always return unhealthy:**
- Check logs for specific health check failures
- Verify all dependencies are accessible
- Test each dependency individually
- Use `/[prefix]/readiness` for detailed status

**Testcontainers timeout:**
- Ensure Docker is running: `docker ps`
- Check Docker resources (memory, CPU)
- Increase timeout if needed
- Use Docker Desktop on Windows/Mac

## Contributing

This library is maintained by the Maliev platform team. For bug reports or feature requests, please create an issue in the repository.

## License

Proprietary - Copyright 2025 MALIEV Co., Ltd. All rights reserved.
