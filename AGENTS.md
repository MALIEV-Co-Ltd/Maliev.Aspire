# Maliev Aspire - Agent Guidelines

This document provides instructions and context for AI agents working on the Maliev Aspire codebase.

## 1. Project Context
- **Framework**: .NET 10.0 (Preview/Bleeding Edge)
- **Platform**: .NET Aspire (Orchestration, Service Defaults)
- **Language**: C# 13+
- **Solution File**: `Maliev.Aspire.slnx` (XML-based solution format)

## 2. Build & Test Commands

### Basic Operations
- **Restore**: `dotnet restore`
- **Build**: `dotnet build`
  - *Note*: `TreatWarningsAsErrors` is ENABLED. All warnings must be fixed.
- **Test**: `dotnet test`

### Running Single Tests
To run a specific test, use the `--filter` flag with the Fully Qualified Name (FQN) or a substring.

```bash
# Run a specific test method
dotnet test --filter "FullyQualifiedName~Maliev.Aspire.Tests.MessagingTests.PaymentService_Publishes_Event"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~Maliev.Aspire.Tests.MessagingTests"
```

## 3. Code Style & Conventions

### Formatting
- **Namespaces**: Use file-scoped namespaces (`namespace Maliev.Aspire.Tests;`).
- **Braces**: Use Allman style (braces on new lines) for methods and control structures.
- **Indentation**: 4 spaces.
- **Line Length**: Aim for < 120 characters, but readability takes precedence.

### Naming
- **Classes/Methods/Properties**: PascalCase (e.g., `ConfigureServices`, `ServiceDatabases`).
- **Private Fields**: `_camelCase` (e.g., `_appFactory`, `_output`).
- **Local Variables**: camelCase (e.g., `iamService`, `builder`).
- **Async Methods**: Suffix with `Async` (e.g., `InitializeAsync`).

### C# Features
- **Implicit Usings**: Enabled. Do not add `using System;` etc., unless necessary to resolve conflicts.
- **Nullable Reference Types**: Enabled. Use `?` for nullable types and handle nulls appropriately.
- **Records**: Use `record` for data transfer objects (DTOs) and configuration holders (e.g., `SharedConfiguration`).

### "Maliev Constitution" (Banned Libraries)
**STRICTLY FORBIDDEN**. The build will fail if these are used.
- ❌ `AutoMapper` (Use manual mapping or fast mappers)
- ❌ `FluentValidation` (Use standard `ComponentModel.DataAnnotations` or manual validation)
- ❌ `FluentAssertions` (Use standard xUnit `Assert` or `Aspire.Hosting.Testing` assertions)
- ❌ `Swashbuckle` (Use `Scalar` instead)

## 4. Architecture & Aspire Patterns

### AppHost
- **Service Registration**: Services are added via `builder.AddProject<T>`.
- **Dependencies**: Use `WithReference` and `WaitFor` to model startup dependencies.
  ```csharp
  var db = builder.AddPostgres("postgres");
  var api = builder.AddProject<Projects.Api>("api")
      .WithReference(db)
      .WaitFor(db); // Critical for startup order
  ```
- **Health Checks**: Always configure `WithHttpHealthCheck`.
- **Secrets**: Use `AddParameterFromConfig` with `secret: true` for sensitive data.

### Testing (`Maliev.Aspire.Tests`)
- **Framework**: xUnit + `Aspire.Hosting.Testing`.
- **Integration Tests**: Tests spin up the full Aspire AppHost.
- **Logging**: Inject `ITestOutputHelper` and use `_output.WriteLine` for test logs.
- **Assertions**: Use strict xUnit `Assert` (e.g., `Assert.NotNull`, `Assert.Equal`).

## 5. Development Workflow
1. **Analyze**: Read `Directory.Build.props` to understand global constraints.
2. **Implement**: Follow existing patterns in `AppHost.cs`.
3. **Verify**: Run `dotnet build` to ensure no warnings (as errors) are introduced.
4. **Test**: Run relevant tests using `dotnet test --filter ...`.

## 6. Common Paths
- `Maliev.Aspire.AppHost/AppHost.cs`: Main orchestration logic.
- `Maliev.Aspire.AppHost/Program.cs`: Configuration extensions (Infrastructure, Services).
- `Maliev.Aspire.Tests/`: Integration tests.
- `Directory.Build.props`: Global build settings and banned packages.


## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure as both project and startup-project (since EF Core Design package is in Infrastructure):
  ```
  dotnet ef migrations add <Name> --project Maliev.<Domain>Service.Infrastructure --startup-project Maliev.<Domain>Service.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
