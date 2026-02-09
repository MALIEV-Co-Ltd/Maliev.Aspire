# Maliev.Aspire Test Specifications

This directory contains the high-level system test specifications for the Maliev Microservices Ecosystem.

## Structure
The specifications are grouped by functional domain:

*   **[FOUNDATION_TESTS.md](./FOUNDATION_TESTS.md)**
    *   Identity (IAM), Authentication (Auth), Registry (Country/Currency), Notifications.
*   **[HR_DOMAIN_TESTS.md](./HR_DOMAIN_TESTS.md)**
    *   Employee Lifecycle, Leave Management.
*   **[COMMERCIAL_DOMAIN_TESTS.md](./COMMERCIAL_DOMAIN_TESTS.md)**
    *   Customer Management, Orders, Invoicing, Payments.
*   **[SUPPLY_CHAIN_TESTS.md](./SUPPLY_CHAIN_TESTS.md)**
    *   Suppliers, Materials, Purchase Orders.

## Usage
These documents serve as the blueprint for implementing Integration Tests in the `Maliev.Aspire.Tests` project using `xUnit` and `Aspire.Hosting.Testing`.
