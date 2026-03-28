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

### Testing (`Maliev.Aspire.Tests`) — System Integration (Tier 3)

This project contains **system integration tests** that verify cross-service workflows against the full Aspire AppHost (all 34 services + Postgres + RabbitMQ + Redis). These are **Tier 3** in the Maliev testing pyramid.

> Full test strategy, coverage matrix, and governance: `Maliev.Aspire.Tests/TEST_PLAN.md`

#### Shared Fixture Pattern (Mandatory)

All tests MUST use the shared `AspireTestFixture` via `[Collection("AspireDomainTests")]`. This starts the AppHost **once** and shares it across all test classes. **Never** create a new `DistributedApplicationFactory` per test class.

```csharp
[Collection("AspireDomainTests")]
public class MyWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task MyTest()
    {
        var client = _fixture.CreateAuthenticatedClient("ServiceName");
        var response = await client.GetAsync("/service/v1/endpoint");
        response.EnsureSuccessStatusCode();
    }
}
```

#### Eventual Consistency (Never Use Task.Delay)

For async operations (e.g., waiting for a RabbitMQ consumer to process an event), use `TestHelpers.WaitForAsync`:

```csharp
// GOOD: Poll until condition met
var response = await TestHelpers.WaitForSuccessAsync(
    () => client.GetAsync($"/notification/v1/delivery-logs?userId={orderId}"),
    timeout: TimeSpan.FromSeconds(30),
    message: "Notification delivery log not created within timeout");

// BAD: Never do this
await Task.Delay(5000);
```

#### Test Organization

```
Maliev.Aspire.Tests/
├── Infrastructure/         # AspireTestFixture, TestHelpers (DO NOT ADD test classes here)
├── Domain/                 # Per-domain workflow tests
│   ├── Commercial/         # Customer, Order, Invoice, Payment, Delivery
│   ├── Communication/      # Notification, PDF
│   ├── Financial/          # Accounting, Currency
│   ├── Foundation/         # Auth, IAM, Country, Registry
│   ├── HR/                 # Career, Compensation, Leave, Lifecycle, Performance
│   ├── People/             # Employee lifecycle
│   ├── SupplyChain/        # Pricing, Supplier, Material, PurchaseOrder
│   └── Workflows/          # Cross-domain multi-service workflows
├── Integration/            # ServiceDiscoveryTests, EventChainTests, ErrorScenarioTests
├── specs/                  # Test specification documents (markdown)
└── TEST_PLAN.md            # Master test strategy document
```

#### What to Test Here (and What NOT to)

**DO test at this level:**
- Cross-service data flows (Order → Payment → Notification)
- Event chains spanning multiple RabbitMQ consumers
- Service discovery and health checks
- End-to-end authentication flows

**DO NOT duplicate at this level:**
- Individual endpoint CRUD operations (covered by per-service `BaseIntegrationTestFactory` tests)
- Single-service business logic (covered by per-service unit tests)
- Permission enforcement per endpoint (covered by per-service contract tests)

#### Assertions & Logging
- **Assertions**: Use strict xUnit `Assert` (e.g., `Assert.NotNull`, `Assert.Equal`). FluentAssertions is banned.
- **Logging**: Use `_output.WriteLine` for test diagnostics (injected via `ITestOutputHelper`).

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


## Git & Version Control — Mandatory Rules

### 🚨 CRITICAL: Always Commit Code Changes (Non-Negotiable)
- **You MUST commit your changes to the local repository after completing any meaningful unit of work.**
- **Never accumulate uncommitted changes.** Do not wait until end of session or until something breaks.
- **Commit early and often** — if a change is meaningful (even a small fix or refactor), commit it.
- **You do NOT need to push to remote** — local commits are sufficient to protect against accidental loss.
- **If you are unsure whether to commit, commit anyway.** Extra commits are harmless; lost work is irreversible.
- This rule applies even if you are just "testing" or "exploring" — use git branches to isolate experimental work and commit those changes too.

### 🚨 CRITICAL: Never Use `git checkout` to Restore Broken Files
- **NEVER use `git checkout` to restore or recover files.** This operation discards uncommitted changes permanently and will result in data loss.
- **To undo/recover from broken files: first commit your current changes, then use `git revert` or `git reset --soft` to safely undo.**

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
