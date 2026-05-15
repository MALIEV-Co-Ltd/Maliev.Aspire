# MALIEV E2E User Journey Run Results

> Dated execution evidence for the production-gate E2E journey catalog and Aspire integrated environment checks.
> Keep the stable story definitions in [E2E_USER_JOURNEY_STORIES.md](./E2E_USER_JOURNEY_STORIES.md); use this file for run results, blockers, and fixes.

## 2026-05-15 Manual Browser E2E Correction Run

### Scope

- Corrective run after confirming that `Maliev.Aspire.Tests` does not yet contain browser E2E automation.
- Manually exercised the Aspire-hosted frontends with browser automation:
  - `Maliev.Web`: `http://localhost:5026`
  - `Maliev.QuoteEngine`: `http://localhost:5012`
  - `Maliev.Intranet`: `http://localhost:5071`
- Used story-level pass, partial, blocked, and failed outcomes. A blocked result means the current integrated environment lacks a required prerequisite, not that the story passed.

### Browser Story Results

| Story ids | Entry point | Browser path verified | Result | Evidence and verification notes |
|-----------|-------------|-----------------------|--------|---------------------------------|
| `WEB-001`, `WEB-010`, `WEB-013` | `http://localhost:5026/`, `/services/3d-printing`, `/quote` | Opened home, service detail, quote landing page, then clicked `Try demo` into QuoteEngine. | Passed after fix | Web displayed services, materials, case studies, blog links, and quote CTAs. After the fix, quote links resolved to `http://localhost:5012/demo` and `http://localhost:5012/projects/new` inside Aspire instead of production `quote.maliev.com`. |
| `WEB-002`, `WEB-012` | `http://localhost:5026/contact` | Opened contact page and verified inquiry form fields. | Partial | Form rendered `Full name`, `Email`, `Phone`, `Company`, `Subject`, `Message`, and `Send message`. Browser submission was not completed because the current browser control failed on HTML `email` inputs; downstream ContactService write still needs an automated test or manual browser input support. |
| `WEB-003`, `WEB-005`, `WEB-006`, `WEB-007`, `WEB-009` | `http://localhost:5026/account`, `/auth/sign-up`, `/auth/forgot-password` | Opened account, sign-up, and reset pages; expanded email sign-up. | Partial | Google primary path, email fallback, forgot-password route, and account copy rendered. Email verification and reset email delivery remain product-gate gaps from the story catalog; form submission was not completed because of the browser email-input limitation. |
| `WEB-008`, `COM-003` | `http://localhost:5026/shop` | Opened storefront and searched visible catalog state. | Partial | Storefront rendered category filter and empty state: `No products listed yet`. This verifies no crash in CommerceService-backed storefront, but cart and checkout draft cannot be completed until test products are seeded or published. |
| `WEB-011` | Footer `Cookie settings` | Opened cookie settings and accepted optional cookies. | Passed | Cookie panel rendered `Continue without optional` and `Accept optional`; clicking `Accept optional` dismissed the banner. |
| `QUOTE-001`, `QUOTE-002`, `QUOTE-003`, `QUOTE-004` | `http://localhost:5012/demo` | Opened QuoteEngine demo, changed process from CNC to FDM, clicked `Estimate`, and verified recalculated pricing. | Passed after fix | Demo loaded `maliev-sample-bracket.step`, DFM messages, viewer slot, material/process controls, and total. Process change temporarily cleared the total, and `Estimate` recalculated to `2,596.00 THB`. |
| `QUOTE-005` | `http://localhost:5012/projects/new` | Opened real project path without auth and opened sign-in prompt. | Passed | Customer-owned upload path showed `Sign in to upload your own files`, Google as primary path, and email/password fallback. Estimate/PDF/sign-in actions were disabled until sign-in. |
| `QUOTE-006`, `QUOTE-007` | `http://localhost:5012/demo` | Attempted formal quote/PDF action in demo mode. | Passed as negative demo guard | `PDF` action was disabled and `Demo only` remained visible, confirming demo mode does not generate formal quotations, PDFs, orders, or customer history. |
| `QUOTE-010` - `QUOTE-015` | QuoteEngine nav: `Orders`, `NDAs`, `Documents`, `Profile` | Verified customer portal navigation exists from shell. | Partial | Portal links are visible, but authenticated history/profile/NDA/document/SignalR journeys require a working test customer login and service-backed replacement for prototype-backed flows. |
| `INT-001`, `SEC-002`, `SEC-003` | `http://localhost:5071/login`, `/`, `/projects/new`, `/admin/system-health` | Opened login and protected employee routes. | Partial | Intranet rendered employee login with email/password and Google. Direct visits to `/projects/new` and `/admin/system-health` redirected to login and preserved return URLs. Employee post-login journeys are blocked because `AspireTestAdminEnabled=false` and no seeded test employee credential is available in the run. |
| `INT-002` - `INT-015`, `MFG-*`, `PROC-*`, `FIN-*`, `HR-*`, `OPS-*` | Intranet authenticated modules | Attempted prerequisite access through protected routes. | Blocked | These stories cannot be manually executed until Aspire provides a deterministic non-production employee test identity and seeded master data. The browser run verified the auth boundary, not the internal workflows. |
| `SEC-001`, `SEC-004` | Customer and employee protected surfaces | Checked anonymous and demo-mode boundaries. | Partial | Anonymous Intranet access is blocked and QuoteEngine demo disables formal artifacts. Cross-customer access and hidden employee-only pricing require at least two seeded customer identities and authenticated QuoteEngine/Intranet browser sessions. |

### Fixed During Manual Browser Run

| Issue | Fix | Verification |
|-------|-----|--------------|
| QuoteEngine BFF served `index.html`, but the Blazor app stayed stuck on `Loading quote engine` during browser verification. | Added `builder.WebHost.UseStaticWebAssets()` to `Maliev.QuoteEngine.Bff` so hosted WASM static assets are available under Aspire. | Browser reload of `http://localhost:5012/demo` rendered QuoteEngine shell, demo sample file, DFM panel, configuration controls, and pricing. `dotnet build Maliev.QuoteEngine.Bff.csproj -p:UseSharedCompilation=false` passed. |
| Web quote CTAs left Aspire and pointed at production `https://quote.maliev.com/...`. | Added a runtime `QuoteEngine__BaseUrl` override in `Maliev.Web` and wired AppHost `WebBff` to `quoteEngineBff.GetEndpoint("http")`. Production default remains `https://quote.maliev.com`. | After restarting AppHost, Web `/quote` rendered `Try demo` as `http://localhost:5012/demo` and `Start project` as `http://localhost:5012/projects/new`; clicking `Try demo` loaded QuoteEngine demo inside Aspire. |

### Manual Browser Blockers

| Blocker | Evidence | Impact | Recommended next action |
|---------|----------|--------|-------------------------|
| No deterministic employee test login is enabled. | Aspire parameter `AspireTestAdminEnabled` is `false`; login-only browser checks could not enter Intranet. | Employee, manufacturing, procurement, finance, HR, admin, and ProjectNew stories cannot be manually completed. | Enable a non-production Aspire test admin seed path with a secret-sourced password, then run authenticated Intranet browser stories. |
| Browser control failed on HTML `email` inputs during form submission. | QuoteEngine and Web sign-up attempts failed when filling `<input type="email">`. | Customer sign-up, reset, and contact submission were verified only to form-render state. | Add real Playwright E2E automation or fix the browser-control input path, then complete customer form submissions. |
| Storefront has no published seeded products. | `/shop` rendered `No products listed yet`. | Cart and checkout draft stories cannot be completed end to end. | Add an Aspire seed command/data set for published CommerceService products. |
| Email/OAuth providers are not represented in the local browser gate. | Sign-up, verification, reset, and Google buttons render, but no local mailbox/OAuth test harness exists. | Email verification and Google sign-in remain production-gate gaps. | Add a local mail sink and OAuth/test-token strategy for Aspire E2E. |

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
