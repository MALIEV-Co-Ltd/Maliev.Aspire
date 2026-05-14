# Maliev Aspire - Agent Guidelines

This document provides instructions and context for AI agents working on the Maliev Aspire codebase.

## 1. Project Context
- **Framework**: .NET 10.0 (Preview/Bleeding Edge)
- **Platform**: .NET Aspire (Orchestration, Service Defaults)
- **Language**: C# 13+
- **Solution File**: `Maliev.Aspire.slnx` (XML-based solution format)

## 2. Build, Test & Lint Commands

All commands run from within this service directory (`B:\maliev\Maliev.Aspire`).

```powershell
# Build (treats warnings as errors — all must be fixed)
dotnet build Maliev.Aspire.slnx

# Run all tests
dotnet test Maliev.Aspire.slnx --verbosity normal

# Run a single test method
dotnet test --filter "FullyQualifiedName~Maliev.Aspire.Tests.MessagingTests.PaymentService_Publishes_Event"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~Maliev.Aspire.Tests.MessagingTests"

# Run with code coverage
dotnet test Maliev.Aspire.slnx --collect:"XPlat Code Coverage"

# Format check
dotnet format Maliev.Aspire.slnx
```

## 3. Code Style & Conventions

### C# Naming & Formatting
- **Namespaces**: File-scoped (`namespace Maliev.Aspire.Tests;`)
- **Classes/Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix)
- **Parameters/locals**: `camelCase`
- **Async methods**: Suffix with `Async` (e.g., `InitializeAsync`)
- **Interfaces**: Prefix with `I` (e.g., `IAuthenticationService`)
- **Permissions**: GCP-style `{domain}.{plural-resource}.{action}` as `public const string` in a `Permissions` static class
  - Valid: `customer.customers.create`, `auth.tokens.revoke`
  - Invalid: `customer.customer.create` (singular), `auth.revoke` (missing resource)
- **XML docs**: Required on ALL public methods and properties
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). Use `?` explicitly
- **Imports**: System first, then third-party, then local. Alphabetize within groups. Remove unused `using`
- **Braces**: Allman style (new line) for methods and control structures. Expression-bodied for properties/accessors
- **Indentation**: 4 spaces, LF line endings, UTF-8, trim trailing whitespace

### C# Patterns
- **DI**: Constructor injection with `private readonly` fields
- **Logging**: `ILogger<T>` with structured placeholders (never interpolate): `_logger.LogInformation("Processing {FileId}", fileId)`
- **Manual mapping**: Static extension methods (`ToDto()`, `ToEntity()`). AutoMapper is banned
- **Validation**: `System.ComponentModel.DataAnnotations` on DTOs. FluentValidation is banned

## 4. Banned Libraries (Build Will Fail)

| Banned | Use Instead |
|--------|-------------|
| AutoMapper | Manual mapping extensions |
| FluentValidation | DataAnnotations or manual validation |
| FluentAssertions | Standard xUnit `Assert.*` |
| Swashbuckle/Swagger | Scalar (at `/{service}/scalar`) |
| InMemoryDatabase (EF Core) | Testcontainers with real PostgreSQL |

## 5. Testing Rules

- **Framework**: xUnit with standard `Assert` (`Assert.Equal`, `Assert.NotNull`, etc.)
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` or `HTTP_METHOD_Path_Scenario_ExpectedStatus`
- **System tests** (Tier 3): `AspireTestFixture` with `[Collection("AspireDomainTests")]` — shared AppHost, never one per class
- **Eventual consistency**: Use `TestHelpers.WaitForAsync`. Never `Task.Delay`
- **MassTransit consumers**: Must have consumer tests using `AddMassTransitTestHarness()`

## 6. Mandatory Rules

- **`TreatWarningsAsErrors = true`**: Zero warnings allowed. No suppression
- **`[RequirePermission("domain.resources.action")]`**: On all endpoints, not plain `[Authorize]`
- **API versioning**: All routes versioned (`v1/`)
- **Service prefix**: Routes prefixed with service domain (e.g., `/auth`, `/customer`, `/job`)
- **Scalar docs**: Configured at `/{service}/scalar`
- **Secrets**: Never hardcoded. Use GCP Secret Manager or environment variables
- **JWT validation**: `AddJwtAuthentication()` must validate RS256 tokens in staging/production. `Jwt:SecurityKey` HS256 validation is Development/Testing fallback only.
- **Service accounts**: Service-account tokens are privileged. Prefer user-context forwarding for user-initiated calls, and require `Jwt:PrivateKey` RS256 signing outside Development/Testing.
- **Async/await**: All the way down. Pass `CancellationToken`
- **EF Core Design package**: Only in Infrastructure project, never in Api
- **PostgreSQL xmin**: Shadow property only — `entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`. Never add entity property
- **Temporary files**: Generate in `/temp` folder, clean up afterwards

## 7. Architecture & Aspire Patterns

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

#### Production-Gate E2E User Stories

`Maliev.Aspire.Tests/specs/E2E_USER_JOURNEY_STORIES.md` is the source of truth for browser-level production gate coverage. Future agents must update it when user-facing customer or employee journeys change.

Use these rules for the E2E story catalog:
- Cover complete journeys through `Maliev.Web`, `Maliev.QuoteEngine`, `Maliev.Intranet`, their BFFs, and downstream services.
- Document persona, entry point, business value, prerequisites, user path, services involved, data created or mutated, verification checklist, observability checks, current implementation status, and known product gaps.
- Keep E2E stories above unit/integration scope. Do not restate endpoint CRUD or single-service behavior unless it proves the user-visible journey.
- Mark missing or prototype-backed behavior explicitly as a product gap. Do not hide it by testing only direct APIs.
- `Maliev.QuoteEngine` is part of the Aspire integrated environment and must remain wired into AppHost for dedicated quoting journeys.
- Quote/project stories must preserve the production contract: Project is the mutable workspace for files, parts, configuration, DFM acknowledgement, pricing, and attachments; Quotation is the project quote family; QuotationVersion is an immutable commercial snapshot with snapshot hash, generated-by identity, change summary, generated timestamp, and exact PDF artifact.
- Do not treat `ProjectNew` as a product concept. It is the Blazor page name for the Intranet employee new project editor/project quote workspace.
- QuoteEngine has two E2E modes: anonymous demo mode is non-mutating and uses MALIEV-owned sample files; signed customer project mode must use server-resolved customer identity and real service-backed Project/Upload/Geometry/Pricing/Quotation/PDF/Order/Payment/Delivery workflows.
- Reorder or major customer changes after acceptance must start from Duplicate Project linked to the source project. Accepted projects, quotation versions, PDFs, orders, and payments must remain immutable evidence.
- Version-aware E2E assertions must prove one quotation per project, multiple immutable quotation versions, exact version PDF links, current-version marker, source-project linkage, and acceptance/order creation against the selected quotation version.

#### Assertions & Logging
- **Assertions**: Use strict xUnit `Assert` (e.g., `Assert.NotNull`, `Assert.Equal`). FluentAssertions is banned.
- **Logging**: Use `_output.WriteLine` for test diagnostics (injected via `ITestOutputHelper`).

## 8. Development Workflow
1. **Analyze**: Read `Directory.Build.props` to understand global constraints.
2. **Implement**: Follow existing patterns in `AppHost.cs`.
3. **Verify**: Run `dotnet build Maliev.Aspire.slnx` to ensure no warnings (as errors) are introduced.
4. **Test**: Run relevant tests using `dotnet test --filter ...`.

## 9. Common Paths
- `Maliev.Aspire.AppHost/AppHost.cs`: Main orchestration logic.
- `Maliev.Aspire.AppHost/Program.cs`: Configuration extensions (Infrastructure, Services).
- `Maliev.Aspire.Tests/`: Integration tests.
- `Directory.Build.props`: Global build settings and banned packages.

## 10. Git Rules

- This is an independent git repo. `cd` into it before git commands.
- **Commit early and often** after every meaningful unit of work. Do not accumulate changes.
- **Never use `git checkout` to restore files** — commit first, then `git revert` or `git reset --soft`.
- Feature branches merged to `develop` via PR. Do not push without being asked.
