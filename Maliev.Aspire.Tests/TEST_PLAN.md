# Maliev Integration Test Plan

> Living document defining the test strategy, coverage matrix, and governance for the Maliev microservices ecosystem.
>
> **Last updated**: 2026-04-01

---

## 1. Testing Strategy

### 1.1 Testing Pyramid

```
                    ┌─────────────┐
                    │     E2E     │  Playwright (Aspire AppHost + Browser)
                    │    Tests    │  ~5 critical user journeys
                    ├─────────────┤
                    │   System    │  Aspire AppHost (all 34 services)
                    │ Integration │  Cross-service workflows, event chains
                    ├─────────────┤
                    │   Service   │  WebApplicationFactory + Testcontainers
                    │ Integration │  Single service API + DB + messaging
                    ├─────────────┤
                    │    Unit     │  In-memory, mocked dependencies
                    │   Tests     │  Business logic, domain models
                    └─────────────┘
```

### 1.2 Tier Definitions

| Tier | Trait Tag | Infrastructure | Scope | Target Time | Location |
|------|-----------|---------------|-------|-------------|----------|
| **Unit** | `[Trait("Tier", "Unit")]` | None (mocks only) | Single class/method | <1s per test | `Maliev.*.Tests/` (per-service repos) |
| **Service Integration** | `[Trait("Tier", "ServiceIntegration")]` | Testcontainers (Postgres + Redis + RabbitMQ) | Single service via `WebApplicationFactory` | <30s per test | `Maliev.*.Tests/` (per-service repos) |
| **System Integration** | `[Trait("Tier", "SystemIntegration")]` | Full Aspire AppHost (all services) | Cross-service workflows, event chains | <2min per test | `Maliev.Aspire.Tests/` |
| **E2E** | `[Trait("Tier", "E2E")]` | Aspire AppHost + Playwright browser | Full user journey through UI | <5min per test | `Maliev.Intranet.E2E/` (future) |

### 1.3 What to Test at Each Tier

**Unit Tests** — per-service repos
- Business logic and domain model behavior
- Value object validation
- Service method logic with mocked dependencies
- Edge cases and error conditions

**Service Integration Tests** — per-service repos (via `BaseIntegrationTestFactory`)
- Individual API endpoint CRUD operations
- Permission enforcement per endpoint (contract tests)
- Database persistence and query correctness
- MassTransit consumer behavior (via test harness)
- Input validation and error responses

**System Integration Tests** — `Maliev.Aspire.Tests/` (this project)
- Cross-service data flows (e.g., Order → Payment → Notification)
- Event chains spanning multiple consumers
- Service discovery and health checks
- Authentication flows end-to-end
- Data consistency across service boundaries
- BFF aggregation layer correctness

**System Integration tests should NOT duplicate:**
- Individual endpoint CRUD (covered by per-service tests)
- Single-service business logic validation
- Permission enforcement per endpoint (covered by per-service contract tests)

**E2E Tests** — future `Maliev.Intranet.E2E/`
- Critical user journeys through the Blazor UI
- Login/authentication flow
- Core business workflows visible to end users

### 1.4 Infrastructure Patterns

| Pattern | Used By | Description |
|---------|---------|-------------|
| `BaseIntegrationTestFactory<TProgram, TDbContext>` | Per-service tests | `WebApplicationFactory` + Testcontainers (Postgres/Redis/RabbitMQ) + JWT auth bypass + MassTransit test harness |
| `AspireTestFixture` | `Maliev.Aspire.Tests` | Shared `DistributedApplicationFactory` starting full AppHost once, shared across all tests via `[Collection("AspireDomainTests")]` |
| `BffTestWebApplicationFactory` | `Maliev.Intranet.Tests` | `WebApplicationFactory` for isolated BFF testing with mocked downstream services |
| `IAMTestHelpers` | All test projects | JWT token creation utility (in `Maliev.Aspire.ServiceDefaults/Testing/`) |
| `TestHelpers.WaitForAsync` | `Maliev.Aspire.Tests` | Async polling helper for eventual consistency assertions |

---

## 2. Coverage Matrix

### 2.1 Per-Service Test Coverage

| # | Service | Test Files | Consumer Tests | Aspire System Test | Notes |
|---|---------|-----------|----------------|-------------------|-------|
| | **Core Services** | | | | |
| 1 | IAMService | 21 | 1 | Identity, ServiceDiscovery | Good |
| 2 | CountryService | 30 | 0 | CountryServiceTests | Good |
| 3 | RegistryService | 10 | 0 | RegistryServiceTests | OK |
| 4 | FacilityService | 39 | 0 | ServiceDiscovery only | Gap: no Aspire workflow test |
| 5 | UploadService | 26 | 2 | ServiceDiscovery only | Gap: no Aspire workflow test |
| | **Auth Services** | | | | |
| 6 | CustomerService | 28 | 2 | CustomerOnboardingTests | Good |
| 7 | EmployeeService | 55 | 0 | EmployeeLifecycleTests | Good |
| 8 | AuthService | 26 | 0 | AuthWorkflowTests | Good |
| | **Commercial** | | | | |
| 9 | OrderService | 27 | 3 | OrderFulfillmentWorkflowTests + EventChainTests | Good |
| 10 | DeliveryService | 17 | 2 | DeliveryWorkflowTests + OrderFulfillmentWorkflowTests | Good |
| 11 | InvoiceService | 25 | 1 | InvoiceWorkflowTests + OrderFulfillmentWorkflowTests + EventChainTests | Good |
| 12 | PaymentService | 28 | 2 | PaymentReceiptWorkflowTests + MessagingTests + EventChainTests | Good |
| 13 | QuotationService | 14 | 0 | QuotationToInvoiceWorkflowTests | OK |
| 14 | ReceiptService | 28 | 0 | PaymentReceiptWorkflowTests + OrderFulfillmentWorkflowTests | Good |
| | **HR** | | | | |
| 15 | CareerService | 49 | 3 | CareerServiceTests | Good |
| 16 | CompensationService | 32 | 2 | CompensationServiceTests | Good |
| 17 | LeaveService | 40 | 4 | LeaveWorkflowTests | Good |
| 18 | LifecycleService | 20 | 0 | LifecycleServiceTests | OK |
| 19 | PerformanceService | 33 | 0 | PerformanceServiceTests | OK |
| | **Supply Chain** | | | | |
| 20 | MaterialService | 10 | 0 | SupplyChainTests | OK |
| 21 | PricingService | 2 | 0 | PricingServiceTests | Gap: low per-service coverage |
| 22 | SupplierService | 14 | 0 | SupplyChainTests | OK |
| 23 | PurchaseOrderService | 10 | 0 | SupplyChainTests | OK |
| 24 | InventoryService | 8 | 2 | InventoryServiceTests | OK — workflow test added |
| | **Communication** | | | | |
| 25 | NotificationService | 39 | 3 | NotificationServiceTests + MessagingTests + EventChainTests | Good |
| 26 | PdfService | 18 | 7 | PdfServiceTests + EventChainTests | Good |
| 27 | ChatbotService | 48 | 0 | None | Gap: no Aspire test |
| | **Financial** | | | | |
| 28 | AccountingService | 23 | 0 | AccountingWorkflowTests | Good |
| 29 | CurrencyService | 46 | 0 | CurrencyServiceTests | Good |
| | **Other** | | | | |
| 30 | ComplianceService | 25 | 3 | ComplianceServiceTests | OK — workflow test added |
| 31 | ContactService | 4 | 0 | ContactServiceTests | OK — workflow test added |
| 32 | JobService | 3 | 0 | JobServiceTests | OK — basic connectivity tests added |
| 33 | ProjectService | 6 | 0 | ProjectServiceTests | OK — CRUD + parts workflow added |
| 34 | PredictionService | 15 | 0 | None | Gap: no Aspire test |
| | **Frontend** | | | | |
| 35 | Intranet BFF | 92 files | 3 | ServiceDiscovery health only | Gap: no E2E tests |
| | **Python** | | | | |
| 36 | GeometryService | N/A | N/A | None | Gap: no tests in Aspire |

### 2.2 Cross-Service Event Chain Coverage

| Event Chain | Status | Test File |
|-------------|--------|-----------|
| Payment → Notification (via RabbitMQ) | **Tested** | `MessagingTests.cs` |
| Invoice Finalized → PDF Generation | **Not tested** | — |
| Customer Created → Propagation to dependent services | **Not tested** | — |
| Employee Created → IAM + Leave + Career provisioning | **Not tested** | — |
| Order → Payment → Delivery full workflow | **Not tested** | — |
| Supplier → Material → PurchaseOrder → Invoice chain | **Not tested** | — |
| File Upload → Preview Images Generated → Order/Project update | **Not tested** | — |

| Event Chain | Status | Test File |
|-------------|--------|-----------|
| Payment → Notification (via RabbitMQ) | **Tested** | `MessagingTests.cs` + `EventChainTests.cs` |
| Invoice Finalized → PDF Generation | **Tested** | `EventChainTests.cs` |
| Customer Created → Propagation to dependent services | **Tested** | `EventChainTests.cs` |
| Employee Created → IAM + Leave + Career provisioning | **Tested** | `EventChainTests.cs` + `EmployeeLifecycleWorkflowTests.cs` |
| Order → Payment → Delivery full workflow | **Tested** | `OrderFulfillmentWorkflowTests.cs` |
| Supplier → Material → PurchaseOrder → Invoice chain | **Tested** | `ProcurementWorkflowTests.cs` + `SupplyChainTests.cs` |
| File Upload → Preview Images Generated → Order/Project update | **Not tested** | — |

### 2.3 Non-Functional Coverage

| Area | Status | Notes |
|------|--------|-------|
| Service health checks (liveness) | **Tested** | `ServiceDiscoveryTests.cs` covers all 26+ services |
| Service readiness checks | **Tested** | 8 core services verified |
| Authentication flow | **Tested** | `AuthWorkflowTests.cs` |
| Authorization (401) | **Tested** | `ErrorScenarioTests.cs` — 401 + 400 scenarios covered |
| Error handling / resilience | **Tested** | `ErrorScenarioTests.cs` — 400 + 404 + malformed request scenarios |
| Performance / load | **Not tested** | — |
| Code coverage reporting | **Not configured** | `coverlet.collector` installed but not reporting |

---

## 3. Test Organization

### 3.1 Directory Structure

```
Maliev.Aspire.Tests/
├── Infrastructure/          # Test base classes, fixtures, helpers
│   ├── AspireTestFixture.cs # Shared AppHost fixture (start once, share)
│   └── TestHelpers.cs       # Async polling, retry, assertion helpers
├── Domain/                  # Business domain workflow tests
│   ├── Commercial/          # Customer, Order, Invoice, Payment, Delivery
│   ├── Communication/       # Notification, PDF
│   ├── Financial/           # Accounting, Currency, Invoice
│   ├── Foundation/          # Auth, IAM, Country, Registry
│   ├── HR/                  # Career, Compensation, Leave, Lifecycle, Performance
│   ├── People/              # Employee lifecycle
│   ├── SupplyChain/         # Pricing, Supplier, Material, PurchaseOrder
│   └── Workflows/           # Cross-domain multi-service workflows (NEW)
├── Integration/             # Infrastructure & cross-cutting tests
│   ├── ServiceDiscoveryTests.cs  # Health checks for all services
│   ├── EventChainTests.cs        # Multi-service event flows (NEW)
│   └── ErrorScenarioTests.cs     # Negative/error tests (NEW)
├── IAM/                     # IAM-specific tests
├── Fixtures/                # Standalone test fixtures (PostgresTestFixture)
├── specs/                   # Test specification documents
│   ├── README.md
│   ├── FOUNDATION_TESTS.md
│   ├── HR_DOMAIN_TESTS.md
│   ├── COMMERCIAL_DOMAIN_TESTS.md
│   ├── SUPPLY_CHAIN_TESTS.md
│   ├── MESSAGING_TESTS.md   # Event chain specs (NEW)
│   └── WORKFLOW_TESTS.md    # Cross-service workflow specs (NEW)
└── TEST_PLAN.md             # This document
```

### 3.2 Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Test class | `{Feature}Tests.cs` or `{Feature}WorkflowTests.cs` | `CustomerOnboardingTests.cs` |
| Test method | `{Action}_{Condition}_{ExpectedResult}` | `CreateOrder_WithValidData_ReturnsCreated` |
| Fixture | `{Scope}Fixture.cs` or `{Scope}TestFixture.cs` | `AspireTestFixture.cs` |
| Helper | `{Purpose}Helpers.cs` | `TestHelpers.cs` |
| Trait tag | `[Trait("Tier", "{TierName}")]` | `[Trait("Tier", "SystemIntegration")]` |

### 3.3 Collection Pattern

All system integration tests MUST use the shared `AspireTestFixture`:

```csharp
[Collection("AspireDomainTests")]
public class MyWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    [Trait("Tier", "SystemIntegration")]
    public async Task MyTest()
    {
        var client = _fixture.CreateAuthenticatedClient("ServiceName");
        // ...
    }
}
```

**Never** inherit from `MalievTestBase` — it starts a new AppHost per test class.

### 3.4 Async Polling Pattern

**Never** use `Task.Delay` for eventual consistency. Use `TestHelpers.WaitForAsync`:

```csharp
// BAD: hardcoded delay
await Task.Delay(5000);
var response = await client.GetAsync("/api/orders/123");

// GOOD: poll until condition met
var response = await TestHelpers.WaitForSuccessAsync(
    () => client.GetAsync("/api/orders/123"),
    timeout: TimeSpan.FromSeconds(30),
    message: "Order was not created within timeout");
```

---

## 4. CI/CD Integration

### 4.1 Pipeline Stages

```
PR Validation Pipeline:
┌──────────────┐     ┌─────────────────────────┐     ┌──────────────┐
│  Build &     │ ──→ │  System Integration     │ ──→ │  Coverage    │
│  NuGet Pack  │     │  Tests (Aspire AppHost)  │     │  Report      │
└──────────────┘     └─────────────────────────┘     └──────────────┘

Per-Service Pipeline (each service repo):
┌──────────────┐     ┌──────────────────────────┐
│  Build       │ ──→ │  Unit + Service           │
│              │     │  Integration Tests         │
└──────────────┘     └──────────────────────────┘

Nightly (future):
┌──────────────────┐
│  E2E Tests       │
│  (Playwright)    │
└──────────────────┘
```

### 4.2 Test Execution Commands

```bash
# Run all Aspire system integration tests
dotnet test Maliev.Aspire.Tests/ -v n

# Run specific tier
dotnet test --filter "Tier=SystemIntegration"

# Run specific domain
dotnet test --filter "FullyQualifiedName~Domain.Commercial"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Run per-service tests (from service repo)
dotnet test Maliev.OrderService.Tests/ -v n
```

---

## 5. Priority Gaps & Roadmap

### 5.1 High Priority (address first)

| Gap | Impact | Action |
|-----|--------|--------|
| No CI/CD test execution | Tests never catch regressions | Add test job to `pr-validation.yml` |
| ~~DONE~~ No event chain tests | Cross-service messaging now verified | `EventChainTests.cs` — 4 tests |
| ~~DONE~~ No cross-service workflow tests | Business flows now verified | `Domain/Workflows/` — 3 files |
| No coverage reporting | Coverage unknown | Configure coverlet + ReportGenerator |
| ~~DONE~~ No error scenario tests at Aspire level | Error handling now verified | `ErrorScenarioTests.cs` — 10 tests |
| ~~DONE~~ InventoryService has no Aspire test | Inventory now tested | `InventoryServiceTests.cs` — 3 tests |
| ChatbotService has no Aspire test | Chatbot integration untested | Add basic connectivity test |
| ~~DONE~~ ComplianceService has no Aspire test | Compliance now tested | `ComplianceServiceTests.cs` — 4 tests |
| ~~DONE~~ ContactService has low coverage | Contact now tested | `ContactServiceTests.cs` — 4 tests |
| ~~DONE~~ JobService has no Aspire test | Job/queue now tested | `JobServiceTests.cs` — 6 tests |
| ~~DONE~~ ProjectService has no Aspire test | Project now tested | `ProjectServiceTests.cs` — 5 tests |

### 5.3 Low Priority (future)

| Gap | Impact | Action |
|-----|--------|--------|
| No E2E browser tests | UI flows unverified | Create `Maliev.Intranet.E2E/` with Playwright |
| No load/performance tests | Performance regressions undetected | Consider k6 or NBomber |
| GeometryService (Python) untested | Python service untested | Add pytest suite in GeometryService repo |
| PricingService has low per-service coverage | Pricing logic gaps | Expand per-service tests |

---

## 6. Governance

### 6.1 Rules for New Code

- **New API endpoint**: Must have at least one service integration test (per-service repo)
- **New MassTransit consumer**: Must have a consumer test using test harness (per-service repo)
- **New cross-service workflow**: Should have an Aspire system integration test
- **New Blazor page/component**: Should have a bUnit component test (Intranet.Tests)

### 6.2 Review Checklist for PRs

- [ ] New endpoints have service integration tests
- [ ] New consumers have consumer tests
- [ ] Cross-service workflows have Aspire-level tests
- [ ] No `Task.Delay` for eventual consistency — use `TestHelpers.WaitForAsync`
- [ ] All new tests use `[Collection("AspireDomainTests")]` for Aspire tests
- [ ] All new tests have `[Trait("Tier", "...")]` tag

### 6.3 Test Maintenance

- This document should be updated when new services are added or coverage changes significantly
- Coverage matrix should be reviewed quarterly
- Test specs in `specs/` should be updated alongside test code changes
