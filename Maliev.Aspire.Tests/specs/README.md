# Maliev.Aspire Test Specifications

This directory contains the high-level system test specifications for the Maliev Microservices Ecosystem.

> For the overall test strategy, testing pyramid, and coverage matrix, see [TEST_PLAN.md](../TEST_PLAN.md).

## Structure
The specifications are grouped by functional domain:

### Production-Gate E2E Stories
*   **[E2E_USER_JOURNEY_STORIES.md](./E2E_USER_JOURNEY_STORIES.md)**
    *   Browser-level customer and employee journeys for the Aspire integrated environment. Covers `Maliev.Web`, `Maliev.QuoteEngine`, `Maliev.Intranet`, and the downstream services that must be proven before production deployment.

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

Use `E2E_USER_JOURNEY_STORIES.md` differently from the API/system specs: it is the production-gate catalog for future Playwright browser tests and should stay focused on complete user journeys rather than lower-level endpoint coverage.
