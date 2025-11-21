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

---

## CI/CD Entities (Added 2025-11-21)

### GitHub Packages
- **Description**: GitHub's package registry used to host the ServiceDefaults NuGet package. Enables cross-repository package consumption.
- **URL**: `https://nuget.pkg.github.com/MALIEV-Co-Ltd/index.json`
- **Authentication**: Requires PAT with `read:packages` scope

### NuGet Package (Maliev.Aspire.ServiceDefaults)
- **Description**: The ServiceDefaults library packaged as a NuGet package for distribution to microservices via GitHub Packages.
- **Package ID**: `Maliev.Aspire.ServiceDefaults`
- **Target Frameworks**: net9.0, net10.0
- **Versioning**: `1.0.{build_number}` or release tag

### nuget.config
- **Description**: Configuration file in each microservice repository that defines NuGet package sources and authentication.
- **Location**: Repository root (e.g., `Maliev.AuthService/nuget.config`)
- **Purpose**: Points to GitHub Packages with credential placeholders for CI

### GITOPS_PAT
- **Description**: Personal Access Token stored as a GitHub repository secret, used for cross-repository operations.
- **Required Scopes**: `read:packages` (for NuGet), `repo` (for GitOps operations)
- **Usage**: Passed as `NUGET_PASSWORD` environment variable during CI

### BuildKit Secret
- **Description**: Docker BuildKit mechanism for passing sensitive data during image builds without storing in layers.
- **Implementation**: `--mount=type=secret,id=name` in Dockerfile, `--secret id=name,env=VAR` in build command
- **Purpose**: Securely pass NuGet credentials during Docker build without security warnings
