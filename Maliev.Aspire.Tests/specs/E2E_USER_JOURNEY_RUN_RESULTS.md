# MALIEV E2E User Journey Run Results

> Dated execution evidence for the production-gate E2E journey catalog and Aspire integrated environment checks.
> Keep the stable story definitions in [E2E_USER_JOURNEY_STORIES.md](./E2E_USER_JOURNEY_STORIES.md); use this file for run results, blockers, and fixes.

## 2026-05-15 Automated Playwright E2E Bootstrap Run

### Scope

- Added executable Playwright browser tests in `Maliev.Aspire.Tests/E2E/BrowserJourneyGateTests.cs`.
- Added catalog traceability checks in `Maliev.Aspire.Tests/E2E/E2EStoryCatalogTraceabilityTests.cs`.
- Installed the matching .NET Playwright Chromium runtime with:
  - `pwsh B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\bin\Debug\net10.0\playwright.ps1 install chromium`

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet build B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj -p:UseSharedCompilation=false -m:1 --no-restore` | Passed |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~E2EStoryCatalogTraceabilityTests"` | Passed: 2 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~BrowserJourneyGateTests"` | Passed: 4 tests |

### Automated Story Coverage Added

| Story ids | Automated browser coverage |
|-----------|----------------------------|
| `WEB-001`, `WEB-010`, `WEB-011`, `WEB-013` | Web home, services, shop, cookie consent, `/quote`, and local QuoteEngine demo handoff. |
| `QUOTE-003`, `QUOTE-024` | QuoteEngine anonymous demo loads the MALIEV sample file, switches to FDM, recalculates price, and keeps formal PDF disabled. |
| `QUOTE-005` | QuoteEngine real project route shows the sign-in/upload gate before customer-owned upload. |
| `INT-001`, `SEC-002`, `SEC-003` | Anonymous Intranet direct access to `/projects/new` redirects to login and preserves return URL. |

### Remaining Full-Catalog Blockers

- Full 95-story browser verification still requires deterministic customer and employee browser sessions, local mail capture for verification/reset links, OAuth test mode, published commerce seed products, and service-backed QuoteEngine project/quotation workflows.
- Stories not listed above remain partial or blocked as recorded in the manual story matrix below. They must not be counted as fully verified until they have automated browser coverage or an explicitly accepted product-gap failure.

## 2026-05-15 Manual Browser E2E Correction Run

### Scope

- Corrective run after confirming that `Maliev.Aspire.Tests` does not yet contain browser E2E automation.
- Manually exercised the Aspire-hosted frontends with browser automation:
  - `Maliev.Web`: `http://localhost:5026`
  - `Maliev.QuoteEngine`: `http://localhost:5012`
  - `Maliev.Intranet`: `http://localhost:5071`
- Used story-level pass, partial, blocked, and failed outcomes. A blocked result means the current integrated environment lacks a required prerequisite, not that the story passed.

### Browser Story Results

| Story id | Result | Browser verification performed | Fix or next required fix |
|----------|--------|------------------------------|--------------------------|
| `WEB-001` | Passed after fix | Opened Web home, service content, and quote CTA. | Fixed Aspire Web CTA to local QuoteEngine URL. |
| `WEB-002` | Partial | Contact page rendered full inquiry form. | Need browser form submission after email-input automation issue is removed. |
| `WEB-003` | Partial | Sign-up page rendered Google primary path and email fallback. | Email verification issue remains a product gap; need local mail sink and submit-capable browser automation. |
| `WEB-004` | Blocked | No email verification token/link flow available to execute. | Implement verification-token issue, email delivery, and verify-link route. |
| `WEB-005` | Partial | Account sign-in page rendered Google, email fallback, reset, and create-account routes. | Need seeded/created customer credentials and form submission. |
| `WEB-006` | Partial | Google customer sign-in CTA rendered. | Need local OAuth/test-token strategy. |
| `WEB-007` | Partial | Forgot-password page rendered reset email form. | Need local mail sink and reset-token browser path. |
| `WEB-008` | Partial | Shop page rendered category/search UI and empty state. | Seed/publish storefront product before cart and checkout draft can pass. |
| `WEB-009` | Partial | Account page rendered customer account entry point. | Need authenticated customer session and seeded profile/order data. |
| `WEB-010` | Passed | Home, service detail, case-study/blog/support links rendered. | No immediate fix. |
| `WEB-011` | Passed | Cookie settings opened; `Accept optional` dismissed consent panel. | No immediate fix. |
| `WEB-012` | Partial | Contact/support information and contact form rendered. | Need contact form submission after email-input automation issue is removed. |
| `WEB-013` | Passed after fix | `/quote` rendered local `Try demo` and `Start project`; clicking `Try demo` loaded QuoteEngine demo. | Fixed Web quote base URL override in Aspire. |
| `QUOTE-001` | Partial | QuoteEngine real project route opened and showed signed-upload gate. | Need authenticated customer session to create/resume real project. |
| `QUOTE-002` | Partial | Demo loaded MALIEV-owned STEP sample, viewer slot, DFM, and metadata. | Need signed customer upload path and GeometryService analysis for customer file. |
| `QUOTE-003` | Passed in demo | Changed process to FDM and clicked `Estimate`; total recalculated to `2,596.00 THB`. | Need repeat against service-backed authenticated project. |
| `QUOTE-004` | Partial | Demo configuration update refreshed estimate after process change. | Need upload/reupload and stale-analysis checks in real project mode. |
| `QUOTE-005` | Passed | Signed-upload gate showed Google primary path and email/password fallback. | Need complete authenticated login and anonymous-work linking. |
| `QUOTE-006` | Partial | Demo correctly disabled formal PDF generation. | Need real ProjectService to QuotationService to PdfService quote-version PDF path. |
| `QUOTE-007` | Blocked | Demo mode blocks order/payment/delivery creation. | Need authenticated accepted quotation version and Omise sandbox checkout path. |
| `QUOTE-008` | Partial | `NDAs` and `Documents` nav exists. | Need authenticated NDA/supporting-document upload and employee visibility checks. |
| `QUOTE-009` | Partial | `Orders` nav exists. | Need authenticated history data and order/manufacturing status records. |
| `QUOTE-010` | Partial | `Profile` nav exists. | Need authenticated customer profile/preferences flow. |
| `QUOTE-011` | Partial | Quote/history shell navigation exists. | Need real project and quotation-version history data. |
| `QUOTE-012` | Partial | `Orders` shell navigation exists. | Need order list/detail records. |
| `QUOTE-013` | Partial | `NDAs` shell navigation exists. | Need NDA upload/list/delete flow. |
| `QUOTE-014` | Partial | `Documents` shell navigation exists. | Need supporting document upload/list/delete flow. |
| `QUOTE-015` | Blocked | SignalR notification journey was not executable anonymously. | Need authenticated customer session and event trigger. |
| `QUOTE-016` | Blocked | Multi-part workspace requires customer file upload. | Need signed customer project with multiple files. |
| `QUOTE-017` | Blocked | Upload failure/retry requires real upload controls and files. | Need signed upload path plus invalid/large file fixtures. |
| `QUOTE-018` | Partial | Demo DFM warnings/info rendered and `DFM reviewed` control exists. | Need real DFM warning fixture and acknowledgement enforcement. |
| `QUOTE-019` | Partial | Lead-time choices rendered with economy/standard/express modifiers. | Need verify deterministic total changes for each lead-time option. |
| `QUOTE-020` | Blocked | Real quote draft editing requires authenticated project. | Need customer login and service-backed draft state. |
| `QUOTE-021` | Blocked | Employee review request requires Intranet/authenticated workflow. | Need employee test login and review request event. |
| `QUOTE-022` | Blocked | Formal quote acceptance requires generated quotation version. | Need quote-version generation and terms/PO UI. |
| `QUOTE-023` | Blocked | Artifact downloads require generated quote/order/manufacturing records. | Need service-backed artifacts and authorization checks. |
| `QUOTE-024` | Passed after fix | Anonymous demo loaded sample file, DFM, pricing, and disabled formal artifacts. | Fixed QuoteEngine static web asset hosting. |
| `QUOTE-025` | Blocked | Multiple quotation versions require signed real project. | Need service-backed project quotation version workflow. |
| `QUOTE-026` | Blocked | Version comparison requires multiple generated quotation versions. | Need version-history UI and seeded/created versions. |
| `INT-001` | Partial | Login page rendered email/password and Google; protected routes redirected. | Need enable Aspire test admin with secret-sourced password and run seeders. |
| `INT-002` | Blocked | Requires authenticated Intranet customer creation module. | Need Aspire test admin and seed/customer prerequisites. |
| `INT-003` | Blocked | Requires authenticated customer detail module. | Need Aspire test admin and customer seed. |
| `INT-004` | Blocked | `/projects/new` redirected to login. | Need Aspire test admin before ProjectNew verification. |
| `INT-005` | Blocked | Requires project quote PDF generation after ProjectNew. | Need authenticated ProjectNew project and PDF artifact. |
| `INT-006` | Blocked | Requires authenticated duplicate/reorder project flow. | Need source project and accepted quote/order state. |
| `INT-007` | Blocked | Requires quotation acceptance into order/job. | Need authenticated project quotation version. |
| `INT-008` | Blocked | Requires invoice/payment/receipt modules. | Need authenticated finance flow and seed records. |
| `INT-009` | Blocked | Requires delivery module and order state. | Need authenticated order/delivery records. |
| `INT-010` | Blocked | Requires IAM admin module. | Need Aspire test admin and permission-scoped UI. |
| `INT-011` | Blocked | Requires role/permission admin module. | Need Aspire test admin. |
| `INT-012` | Blocked | Requires material master-data module. | Need Aspire test admin and material seed. |
| `INT-013` | Blocked | Requires equipment/facility module. | Need Aspire test admin and facility seed. |
| `INT-014` | Blocked | Requires authenticated dashboard overview. | Need Aspire test admin and seeded work. |
| `INT-015` | Blocked | Requires authenticated chat/AI assistant. | Need Aspire test admin and tool callback fixture. |
| `INT-016` | Blocked | Requires authenticated project draft autosave. | Need Aspire test admin and ProjectNew access. |
| `INT-017` | Blocked | Requires authenticated multi-file resumable upload. | Need Aspire test admin and CAD file fixtures. |
| `INT-018` | Blocked | Requires project viewer/thumbnails/drawings/DFM after upload. | Need authenticated project upload and GeometryService artifacts. |
| `INT-019` | Blocked | Requires authenticated project pricing. | Need ProjectNew access and pricing reference data. |
| `INT-020` | Blocked | Requires authenticated bulk part editing. | Need multi-part project fixture. |
| `INT-021` | Blocked | Requires authenticated DFM issue/retry path. | Need failing DFM fixture. |
| `INT-022` | Blocked | Requires commercial terms and draft PDF. | Need authenticated ProjectNew quote workspace. |
| `INT-023` | Blocked | Requires formal quotation version creation. | Need project workspace and QuotationService version path. |
| `INT-024` | Blocked | Requires temp file migration and persisted artifact ownership. | Need upload fixture and persistence checks. |
| `INT-025` | Blocked | Requires part drawing/supporting attachment flow. | Need authenticated project part attachment path. |
| `INT-026` | Blocked | Requires duplicate-part action in project draft. | Need authenticated ProjectNew draft. |
| `INT-027` | Blocked | Requires pricing/geometry/SignalR failure triggers. | Need authenticated project and controllable service failures. |
| `INT-028` | Blocked | Requires quotation revision history. | Need multiple quotation versions. |
| `COM-001` | Blocked | Requires authenticated Intranet catalog product editor. | Need Aspire test admin. |
| `COM-002` | Blocked | Requires publishing product from Intranet and Web visibility check. | Need Aspire test admin and product seed/editor. |
| `COM-003` | Partial | Web storefront rendered empty category/search state. | Seed/publish product so browse/detail/cart/checkout can pass. |
| `COM-004` | Blocked | Requires product archive/unpublish flow. | Need authenticated catalog admin and published product. |
| `OPS-001` | Blocked | Requires authenticated global search. | Need Aspire test admin and searchable seed data. |
| `OPS-002` | Blocked | `/admin/system-health` redirected to login. | Need Aspire test admin; prior system tests cover health APIs only. |
| `OPS-003` | Blocked | Requires authenticated notifications. | Need Aspire test admin and event trigger. |
| `FIN-001` | Blocked | Requires authenticated invoice creation. | Need Aspire test admin and customer/order seed. |
| `FIN-002` | Blocked | Requires authenticated invoice/payment status update. | Need invoice/payment seed and Omise sandbox path. |
| `MFG-001` | Blocked | Requires authenticated manufacturing scheduling. | Need Aspire test admin and job/equipment seed. |
| `MFG-002` | Blocked | Requires shop-floor mobile authenticated job. | Need test employee role and assigned job. |
| `MFG-003` | Blocked | Requires equipment/work-center assignment UI. | Need facility/equipment/job seed. |
| `MFG-004` | Blocked | Requires material reservation/consumption UI. | Need inventory/material/job seed. |
| `MFG-005` | Blocked | Requires manufacturing status event/SignalR. | Need authenticated job status event trigger. |
| `PROC-001` | Blocked | Requires authenticated PO creation. | Need Aspire test admin and supplier/material seed. |
| `PROC-002` | Blocked | Requires supplier profile UI. | Need Aspire test admin. |
| `PROC-003` | Blocked | Requires PO detail/cancel/attachment UI. | Need created PO and authenticated session. |
| `PROC-004` | Blocked | Requires receiving and inventory/accounting impact. | Need PO, inventory, and accounting seed path. |
| `HR-001` | Blocked | Requires employee lifecycle module. | Need Aspire test admin with HR/admin permissions. |
| `HR-002` | Blocked | Requires leave request and manager approval. | Need two employee identities and role-scoped sessions. |
| `HR-003` | Blocked | Requires career candidate module. | Need authenticated HR user and candidate seed. |
| `HR-004` | Blocked | Requires compliance/training module. | Need authenticated HR/compliance user and records. |
| `HR-005` | Blocked | Requires compensation module. | Need authenticated HR/finance user and permission boundary checks. |
| `HR-006` | Blocked | Requires performance module. | Need manager/employee identities and review records. |
| `SEC-001` | Blocked | Cross-customer denial requires two authenticated customers. | Need two seeded customer accounts and QuoteEngine/Web sessions. |
| `SEC-002` | Partial | Anonymous direct URLs to restricted Intranet pages redirected to login. | Need low-permission employee session to verify 403/hidden modules. |
| `SEC-003` | Partial | Direct protected URLs preserved return URL on login redirect. | Need expired authenticated session check. |
| `SEC-004` | Partial | QuoteEngine demo hides formal/internal artifacts and disables PDF. | Need authenticated customer surfaces plus employee quote with internal pricing. |

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
