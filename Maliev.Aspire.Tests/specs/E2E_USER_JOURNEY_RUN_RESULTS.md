# MALIEV E2E User Journey Run Results

> Dated execution evidence for the production-gate E2E journey catalog and Aspire integrated environment checks.
> Keep the stable story definitions in [E2E_USER_JOURNEY_STORIES.md](./E2E_USER_JOURNEY_STORIES.md); use this file for run results, blockers, and fixes.

## 2026-05-15 Run

### Scope

- Requested: run all available E2E tests, document the results, and fix issues found during the run.
- Browser E2E automation status: no implemented Playwright/browser E2E tests are currently present in `Maliev.Aspire.Tests`; `Tier=E2E` discovery matched zero tests.
- Executed available service-level end-to-end tests, QuoteEngine journey smoke tests, and focused Aspire system integration checks.
- Did not re-run the full Aspire suite after the known long-running blockers were isolated because the full run timed out before completion.

### Result Summary

| Area | Command or filter | Result | Evidence and coverage |
|------|-------------------|--------|-----------------------|
| Aspire browser E2E discovery | `dotnet test Maliev.Aspire.Tests.csproj --no-build --list-tests --filter "Tier=E2E"` | No tests matched | Confirms the story catalog is not yet backed by Playwright automation. |
| Currency service E2E/final controller tests | `FullyQualifiedName~FinalRatesControllerTests\|FinalCurrenciesControllerTests\|FinalSnapshotsControllerTests\|FinalSystemControllerTests` | Passed, 39/39 | Rates, currencies, snapshots, and system endpoints. |
| Upload service E2E tests | `FullyQualifiedName~EndToEndTests` | Passed, 3/3 | Upload, retrieve, delete, isolation, and resumable large upload paths. |
| Prediction service E2E tests | `FullyQualifiedName~PredictionsEndToEndTests` | Passed, 7/7 | Print-time prediction, demand forecast, cache hit/miss behavior. |
| QuoteEngine endpoint journey smoke tests | `FullyQualifiedName~QuoteEngineEndpointTests` | Passed, 6/6 | Reference data, non-mutating demo, signed upload gate, content-range upload, quote estimate gating, server-side profile identity. |
| Aspire reference and fixture unit checks | `FullyQualifiedName~AppHostReferenceTests\|Unit\|IAMRegistrationHealthCheckTests\|BackgroundIAMRegistrationServiceTests\|UrlQueryRedactionProcessorTests` | Passed, 30/30 | AppHost references, IAM registration helpers, and URL query redaction. |
| Aspire AppHost reference tests | `FullyQualifiedName~AppHostReferenceTests` | Passed, 10/10 | QuoteEngine and Web BFF wiring guardrails. |
| Aspire AppHost build | `dotnet build Maliev.Aspire.AppHost.csproj --no-dependencies` | Passed, 0 warnings/errors | Confirms AppHost compile after Omise parameter fallback and fixture changes. |
| Aspire core readiness checks | `FullyQualifiedName~CoreService_PassesReadinessCheck` | Passed, 8/8 | IAM, Auth, Customer, Employee, Country, Order, Payment, and Invoice readiness. |
| Aspire Geometry service checks | `FullyQualifiedName~GeometryServiceTests` | Passed, 3/3 | Geometry liveness, Scalar documentation endpoint, and protected-route behavior. |
| Aspire full test suite | `dotnet test Maliev.Aspire.Tests.csproj --no-build` | Timed out after 20 minutes | Full-suite execution currently cannot be used as a clean production gate. |
| Aspire service discovery | `FullyQualifiedName~Integration.ServiceDiscoveryTests` | Passed, 39/39 | PaymentService, IAM readiness, GeometryService liveness, and Intranet system-health checks are green after fixes. |

### Fixed During This Run

| Issue | Fix | Verification |
|-------|-----|--------------|
| `PaymentService` blocked Aspire startup when `PaymentProviders:Omise:WebhookSecret` was not configured locally. | `AppHost.cs` now gives `OmiseWebhookSecret` a local/testing fallback through `AddParameterFromConfig` while still allowing configured secrets to override it. | PaymentService was no longer present in the latest failing `ServiceDiscoveryTests` result. AppHost build and AppHost reference tests passed. |
| Aspire fixture liveness timing was too short for slow local container startup. | `AspireTestFixture` now gives `PaymentService` and `GeometryService` longer startup windows. | Focused AppHost reference tests passed; final ServiceDiscovery passed. |
| Aspire fixture only tried HTTPS endpoint lookup before falling back to generic client creation. | `AspireTestFixture` now tries explicit HTTP endpoint lookup for HTTP-only resources before the generic fallback, with Geometry using the factory client path. | AppHost reference tests and build passed; final ServiceDiscovery passed. |
| `GeometryService` was blocked by `otelcollector` failure during Aspire test startup. | `GeometryService` still receives the OTLP endpoint, but only waits for `otelcollector` outside Testing. This keeps normal local startup ordering while preventing optional observability from blocking the test gate. | `GeometryServiceTests` passed 3/3 and `ServiceDiscoveryTests` passed 39/39. |
| Geometry test clients could be routed through an unhealthy fixed DCP port. | Removed the fixed host port from the Geometry AppHost endpoint and kept only `targetPort: 8081`; the Aspire test fixture uses the factory client base address for Geometry. | `GeometryService_Liveness_ReturnsOk` passed after rebuild. |

### Remaining Blockers

| Blocker | Evidence | Impact | Recommended next action |
|---------|----------|--------|-------------------------|
| Browser E2E automation is not implemented yet. | `Tier=E2E` discovery matched zero tests. | `E2E_USER_JOURNEY_STORIES.md` is ready as the production-gate catalog, but the user journeys cannot yet be executed in a browser gate. | Add Playwright suites for `Maliev.Web`, `Maliev.QuoteEngine`, and `Maliev.Intranet`; each test should cite one or more story ids. |
| Full Aspire suite timed out. | `dotnet test Maliev.Aspire.Tests.csproj --no-build` exceeded 20 minutes without a final result. | Full suite is not currently usable as a deterministic deployment gate. | Run narrower suites while fixing IAM/Geometry; then re-enable the full suite as a final gate once service-discovery is green. |

### Production-Gate Interpretation

- Current automated evidence supports several service-level journeys, QuoteEngine BFF smoke paths, and AppHost wiring.
- The documented customer and employee browser journeys are not yet executable as E2E tests.
- The next production-gate milestone should be a small Playwright foundation that starts Aspire once, opens the three frontends, signs in through the supported test identities, and records story-id level pass/fail evidence here.
