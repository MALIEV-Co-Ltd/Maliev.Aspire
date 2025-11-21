# Feature Specification: Maliev.Aspire Local Development Orchestrator

**Feature Branch**: `001-aspire-orchestrator`
**Created**: 2025-01-21
**Updated**: 2025-11-21
**Status**: In Progress
**Input**: User description: "Generate a detailed technical specification for the Maliev.Aspire project based on the correct secret management structure."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Single-Command Environment Startup (Priority: P1)

A developer clones the Maliev monorepo and wants to start working immediately. They need to launch the entire microservice ecosystem with a single command, without manually starting containers, configuring databases, or coordinating service startup order.

**Why this priority**: This is the core value proposition of the solution. Without single-command startup, developers face significant onboarding friction and environment inconsistencies.

**Independent Test**: Can be fully tested by running `dotnet run` from the AppHost directory and verifying all services and infrastructure containers are accessible.

**Acceptance Scenarios**:

1. **Given** a developer has cloned the repository and created `sharedsecrets.json`, **When** they run `dotnet run` from the AppHost project directory, **Then** all infrastructure containers (PostgreSQL, RabbitMQ, Redis) start automatically and become accessible.
2. **Given** all infrastructure containers are running, **When** the AppHost continues startup, **Then** all microservices start and register with the Aspire Dashboard.
3. **Given** the full environment is running, **When** the developer accesses the Aspire Dashboard URL, **Then** they see all services with health status indicators.

---

### User Story 2 - Shared Configuration Injection (Priority: P2)

A developer needs all microservices to receive common configuration values (JWT settings, CORS origins, message queue connection details) without duplicating configuration across multiple service projects.

**Why this priority**: Eliminates configuration duplication and ensures consistency across services. Critical for JWT validation and cross-service communication.

**Independent Test**: Can be verified by checking environment variables on any running service and confirming shared values match `sharedsecrets.json`.

**Acceptance Scenarios**:

1. **Given** shared configuration values are defined in `sharedsecrets.json`, **When** the AppHost starts a microservice, **Then** the service receives those values as environment variables.
2. **Given** a service needs RabbitMQ connection details, **When** the service reads `RabbitMq__Host` and `RabbitMq__Port`, **Then** it receives the values from the shared configuration.
3. **Given** JWT public key is configured in shared secrets, **When** any service validates a JWT token, **Then** all services use the identical public key.

---

### User Story 3 - Service-Specific Secret Isolation (Priority: P3)

A developer working on the AuthService needs to configure a private JWT signing key that only that service should access. Other services should not have access to this sensitive credential.

**Why this priority**: Security best practice - least privilege. Private keys and service-specific database credentials should not be exposed to unrelated services.

**Independent Test**: Can be verified by checking that AuthService has access to `Jwt__PrivateKey` while other services do not.

**Acceptance Scenarios**:

1. **Given** a developer sets `Jwt__PrivateKey` via user-secrets for AuthService, **When** AuthService starts, **Then** it can access the private key for signing tokens.
2. **Given** the private key is set only in AuthService user-secrets, **When** CustomerService starts, **Then** it cannot access `Jwt__PrivateKey`.
3. **Given** each service has its own database connection string in user-secrets, **When** services start, **Then** each service connects only to its designated database.

---

### User Story 4 - Standardized Observability (Priority: P4)

A developer debugging a cross-service request needs to trace the flow through multiple services. All services should automatically provide health endpoints, structured logging with correlation IDs, and telemetry data to the Aspire Dashboard.

**Why this priority**: Essential for debugging distributed systems but not required for basic functionality.

**Independent Test**: Can be verified by calling `/health` endpoints on services and viewing traces in the Aspire Dashboard.

**Acceptance Scenarios**:

1. **Given** a service references ServiceDefaults, **When** the service starts, **Then** it exposes `/health` and `/alive` endpoints returning appropriate status.
2. **Given** a request flows through multiple services, **When** viewing the Aspire Dashboard, **Then** the trace shows the complete request flow with timing.
3. **Given** a service logs an event, **When** viewing logs in the dashboard, **Then** logs include correlation IDs linking related requests.

---

### Edge Cases

- What happens when `sharedsecrets.json` is missing or malformed?
  - System should fail fast with a clear error message indicating the missing configuration file
- What happens when a referenced microservice project does not exist?
  - Build should fail with clear indication of which project reference is invalid
- What happens when infrastructure containers fail to start (e.g., Docker not running)?
  - System should report container startup failures clearly in console output and dashboard
- What happens when a service-specific user-secret is not set?
  - Service should start but fail gracefully when accessing the missing configuration, with clear logging
- What happens when port conflicts occur (e.g., PostgreSQL port 5432 already in use)?
  - System should report the conflict and suggest resolution (stop conflicting process or change port)

### User Story 5 - CI/CD Integration with ServiceDefaults Package (Priority: P1)

Each microservice has its own Git repository with independent CI/CD pipelines. The CI builds must be able to reference ServiceDefaults without access to the Aspire project source code.

**Why this priority**: Critical for CI/CD. Without this, all microservice CI pipelines fail because they cannot resolve the ServiceDefaults project reference.

**Independent Test**: Push a commit to any microservice repo and verify the CI pipeline completes successfully.

**Acceptance Scenarios**:

1. **Given** ServiceDefaults is published as a NuGet package to GitHub Packages, **When** a microservice CI runs `dotnet restore`, **Then** the package is successfully restored from GitHub Packages.
2. **Given** a microservice Dockerfile runs `dotnet restore`, **When** Docker builds the image, **Then** the restore succeeds using BuildKit secrets for authentication.
3. **Given** the ServiceDefaults package is updated, **When** a new version is published, **Then** microservices can update their package reference to use the new version.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a single entry point (`dotnet run` on AppHost) that starts all infrastructure and services
- **FR-002**: System MUST start PostgreSQL, RabbitMQ, and Redis containers automatically
- **FR-003**: System MUST read shared configuration from `sharedsecrets.json` in the AppHost project directory
- **FR-004**: System MUST inject shared configuration values as environment variables into all services
- **FR-005**: System MUST allow services to use .NET User Secrets for service-specific credentials
- **FR-006**: System MUST provide an Aspire Dashboard for monitoring all services
- **FR-007**: Services MUST expose `/health` and `/alive` endpoints via ServiceDefaults
- **FR-008**: Services MUST send telemetry (logs, traces, metrics) to the Aspire Dashboard
- **FR-009**: System MUST support adding new microservices by adding project references and configuration in AppHost
- **FR-010**: System MUST exclude `sharedsecrets.json` from version control via `.gitignore`
- **FR-011**: System MUST provide connection references to infrastructure resources via `.WithReference()` method

### Key Entities

- **AppHost**: The orchestration project that defines and manages all infrastructure and service resources. Contains the `Program.cs` with resource definitions.
- **ServiceDefaults**: A shared library providing standardized configuration for health checks, logging, and telemetry. All services reference this.
- **Infrastructure Resource**: A containerized dependency (PostgreSQL, RabbitMQ, Redis) managed by Aspire.
- **Microservice**: An individual API project from the Maliev monorepo orchestrated by the AppHost.
- **Shared Secrets**: Configuration values common to all services, stored in `sharedsecrets.json`.
- **Service Secrets**: Configuration values specific to a single service, stored in .NET User Secrets.

### Orchestrated Services (20 total)

| Service | Project Path (relative to Maliev.Aspire) | Description |
|---------|------------------------------------------|-------------|
| Maliev.AuthService | `../Maliev.AuthService/Maliev.AuthService.Api` | Authentication and authorization |
| Maliev.CareerService | `../Maliev.CareerService/Maliev.CareerService.Api` | Career/job management |
| Maliev.ChatbotService | `../Maliev.ChatbotService/Maliev.ChatbotService.Api` | Chatbot integration |
| Maliev.ContactService | `../Maliev.ContactService/Maliev.ContactService.Api` | Contact management |
| Maliev.CountryService | `../Maliev.CountryService/Maliev.CountryService.Api` | Country/region data |
| Maliev.CurrencyService | `../Maliev.CurrencyService/Maliev.CurrencyService.Api` | Currency management |
| Maliev.CustomerService | `../Maliev.CustomerService/Maliev.CustomerService.Api` | Customer management |
| Maliev.EmployeeService | `../Maliev.EmployeeService/Maliev.EmployeeService.Api` | Employee management |
| Maliev.InvoiceService | `../Maliev.InvoiceService/Maliev.InvoiceService.Api` | Invoice processing |
| Maliev.MaterialService | `../Maliev.MaterialService/Maliev.MaterialService.Api` | Material/inventory management |
| Maliev.OrderService | `../Maliev.OrderService/Maliev.OrderService.Api` | Order processing |
| Maliev.PaymentService | `../Maliev.PaymentService/Maliev.PaymentService.Api` | Payment processing |
| Maliev.PdfService | `../Maliev.PdfService/Maliev.PdfService.Api` | PDF generation |
| Maliev.PredictionService | `../Maliev.PredictionService/Maliev.PredictionService.Api` | ML predictions |
| Maliev.PurchaseOrderService | `../Maliev.PurchaseOrderService/Maliev.PurchaseOrderService.Api` | Purchase order management |
| Maliev.QuotationRequestService | `../Maliev.QuotationRequestService/Maliev.QuotationRequestService.Api` | Quotation requests |
| Maliev.QuotationService | `../Maliev.QuotationService/Maliev.QuotationService.Api` | Quotation management |
| Maliev.ReceiptService | `../Maliev.ReceiptService/Maliev.ReceiptService.Api` | Receipt processing |
| Maliev.SupplierService | `../Maliev.SupplierService/Maliev.SupplierService.Api` | Supplier management |
| Maliev.UploadService | `../Maliev.UploadService/Maliev.UploadService.Api` | File upload handling |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Developer can start the complete environment in under 5 minutes from a fresh clone (excluding container image downloads)
- **SC-002**: All 20 microservices start successfully and show healthy status in the Aspire Dashboard
- **SC-003**: Environment startup requires exactly one command (`dotnet run`) after initial configuration
- **SC-004**: 100% of shared configuration values are consistently available across all services
- **SC-005**: Service-specific secrets remain isolated to their respective services
- **SC-006**: Health check endpoints respond within 1 second for all services
- **SC-007**: Request traces span multiple services and are visible in the dashboard
- **SC-008**: New developer onboarding time reduced compared to manual environment setup

## Clarifications

### Session 2025-01-21

- Q: Which microservices exist in the Maliev monorepo? → A: 20 services confirmed (see Orchestrated Services below)

Specification coverage assessment:
- Functional scope and success criteria: Clear
- Configuration layering model (shared + service-specific secrets): Clear
- Infrastructure resources (PostgreSQL, RabbitMQ, Redis): Clear
- Observability requirements: Clear
- Edge cases and error handling: Clear
- Service inventory: Clear (20 services enumerated)

Ready to proceed to planning phase.

## Assumptions

- Docker Desktop or compatible container runtime is installed and running on developer machines
- Developers have .NET 8+ SDK installed
- The 20 Maliev microservices listed above exist and are compatible with .NET Aspire orchestration
- Each microservice already has or will add a reference to `Maliev.Aspire.ServiceDefaults`
- Each service has its own database and manages its connection string via .NET User Secrets
