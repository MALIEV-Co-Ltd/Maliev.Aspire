# Data Model: Maliev.Aspire Orchestrator

This document outlines the key conceptual entities for the Maliev.Aspire orchestrator. This feature does not introduce new database schemas but instead defines the components of the orchestration system itself.

### AppHost
- **Description**: The primary .NET Aspire project that defines and manages all infrastructure and service resources. It is the executable entry point for the local development environment.
- **Attributes**: Contains the `Program.cs` file where the application's distributed resource model is built.

### ServiceDefaults
- **Description**: A shared class library that provides standardized, reusable configurations for cross-cutting concerns like health checks, logging, and telemetry.
- **Relationships**: Referenced by all 20 orchestrated microservices to ensure consistent observability.

### Infrastructure Resource
- **Description**: A containerized dependency (e.g., database, message queue, cache) managed by the Aspire AppHost.
- **Examples**: A PostgreSQL container, a RabbitMQ container, a Redis container.

### Microservice Resource
- **Description**: An individual .NET API project from the Maliev monorepo that is registered with and managed by the AppHost.
- **Relationships**: Registered with the `AppHost` and typically references the `ServiceDefaults` library.

### Shared Secrets
- **Description**: A configuration file (`sharedsecrets.json`) containing values common to all services, primarily infrastructure connection strings. This file is explicitly excluded from source control.
- **Scope**: Loaded only by the `AppHost` and its values are injected into the microservices.

### Service Secrets
- **Description**: Configuration values that are specific and private to a single service (e.g., a private signing key for `AuthService`).
- **Implementation**: Managed via the standard .NET User Secrets mechanism, ensuring isolation between services.
