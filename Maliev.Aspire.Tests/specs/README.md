# Maliev.Aspire Test Specifications

This directory contains the high-level system test specifications for the Maliev Microservices Ecosystem.

> For the overall test strategy, testing pyramid, and coverage matrix, see [TEST_PLAN.md](../TEST_PLAN.md).

## Structure
The specifications are grouped by functional domain:

### Production-Gate E2E Stories
*   **[E2E_USER_JOURNEY_STORIES.md](./E2E_USER_JOURNEY_STORIES.md)**
    *   Browser-level customer and employee journeys for the Aspire integrated environment. Covers `Maliev.Web`, `Maliev.QuoteEngine`, `Maliev.Intranet`, and the downstream services that must be proven before production deployment.
    *   Quote/project stories use the production model: Project is the mutable workspace, Quotation is the project quote family, QuotationVersion is the immutable commercial snapshot with exact PDF artifact, and acceptance/order creation must reference the selected version.
*   **[E2E_USER_JOURNEY_RUN_RESULTS.md](./E2E_USER_JOURNEY_RUN_RESULTS.md)**
    *   Dated execution evidence for the production-gate journey catalog, including commands run, pass/fail status, blockers, and follow-up actions.
    *   Use this for verification results. Do not store run results inside the stable story catalog.

### Domain Test Specs
*   **[FOUNDATION_TESTS.md](./FOUNDATION_TESTS.md)**
    *   Identity (IAM), Authentication (Auth), Registry (Country/Currency), Notifications.
*   **[HR_DOMAIN_TESTS.md](./HR_DOMAIN_TESTS.md)**
    *   Employee Lifecycle, Leave Management.
*   **[COMMERCIAL_DOMAIN_TESTS.md](./COMMERCIAL_DOMAIN_TESTS.md)**
    *   Customer Management, Orders, Invoicing, Payments.
*   **[SUPPLY_CHAIN_TESTS.md](./SUPPLY_CHAIN_TESTS.md)**
    *   Suppliers, Materials, Purchase Orders.

### Cross-Cutting Test Specs
*   **[MESSAGING_TESTS.md](./MESSAGING_TESTS.md)**
    *   Event chain tests: RabbitMQ event flows spanning multiple services.
*   **[WORKFLOW_TESTS.md](./WORKFLOW_TESTS.md)**
    *   Cross-service workflow tests: multi-step business processes via HTTP APIs.

## Usage
These documents serve as the blueprint for implementing Integration Tests in the `Maliev.Aspire.Tests` project using `xUnit` and `Aspire.Hosting.Testing`.

Use `E2E_USER_JOURNEY_STORIES.md` differently from the API/system specs: it is the production-gate catalog for future Playwright browser tests and should stay focused on complete user journeys rather than lower-level endpoint coverage. Record dated pass/fail evidence in `E2E_USER_JOURNEY_RUN_RESULTS.md`. If no browser automation exists yet, run manual browser/Playwright checks against the Aspire-hosted frontends and record story-level pass, partial, blocked, or failed results; do not substitute service/system integration tests for browser E2E evidence. For QuoteEngine, keep anonymous demo stories non-mutating and signed customer stories service-backed through Project, Upload, Geometry, Pricing, Quotation, PDF, Order, Payment, and Delivery services. For Intranet, refer to the employee new project editor/project quote workspace instead of treating `ProjectNew` as a product concept.
