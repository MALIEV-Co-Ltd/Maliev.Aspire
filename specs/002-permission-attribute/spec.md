# Feature Specification: Permission Enforcement and IAM Registration

**Feature Branch**: `002-permission-attribute`  
**Created**: 2025-12-21  
**Status**: Draft  
**Input**: `servicedefaults-specify.md` and `servicedefaults-plan.md`

## Overview
Add shared IAM authorization components to `Maliev.Aspire.ServiceDefaults` that all business services can use for permission-based authorization and IAM registration.

## Clarifications

### Session 2025-12-21
- Q: If the IAM service is unavailable during registration, should the microservice block its own startup or log and continue? → A: Log the error and allow the service to continue starting up (Fail-open for availability).
- Q: How should multiple permissions in a single attribute be handled? → A: Any permission (OR logic) - User MUST have at least one of the listed permissions.
- Q: Should the permission matching support wildcards? → A: Support wildcards (e.g., `invoice.*` matches `invoice.create`).
- Q: What if the IAM service endpoint is not configured (e.g., in local development)? → A: Silent failure (Log warning, but allow service to run without registration).
- Q: How should microservices identify their own "service name" for registration? → A: Read from configuration (`ExternalServices:IAM:ServiceName` or passed to base constructor).
- Q: How should the IAM service handle registration updates? → A: Strictly additive (IAM only adds new permissions; removal is manual).
- Q: What is the functional impact of the `IsCritical` flag in `PermissionRegistration`? → A: Trigger enhanced audit logging on every access check.
- Q: How should microservices handle invalid permission formats during the registration phase? → A: Throw exception on startup (Fast-fail during initialization).
- Q: What specific retry policy should be used for registration? → A: Exponential backoff (e.g., 2s, 4s, 8s...).
- Q: What key fields should be included in the 'enhanced audit log' for critical permissions? → A: PrincipalId, ClientId, IP Address, PermissionId.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Secure Controller Action (Priority: P1)

As a developer, I want to restrict access to a specific API endpoint based on user permissions by adding a simple attribute to the controller action, so that I can enforce fine-grained access control.

**Acceptance Scenarios**:
1. **Given** an endpoint `[RequirePermission("invoice.invoices.create")]`, **When** a user has the "invoice.invoices.create" claim, **Then** access is granted.
2. **Given** an endpoint `[RequirePermission("invoice.invoices.create")]`, **When** a user is unauthenticated, **Then** return 401 Unauthorized.
3. **Given** an endpoint `[RequirePermission("invoice.invoices.create")]`, **When** an authenticated user lacks the claim, **Then** return 403 Forbidden.

### User Story 2 - Automated IAM Registration (Priority: P2)

As a system architect, I want services to register their capabilities with IAM on startup.

**Acceptance Scenarios**:
1. **Given** a service implementation of `IAMRegistrationService`, **When** the service starts, **Then** it sends POST requests to `/api/v1/permissions/register` and `/api/v1/roles/register`.
2. **Given** IAM is down, **When** registration fails, **Then** the service logs an error and continues startup.

### User Story 3 - Case-Insensitive & Wildcard Matching (Priority: P3)

**Acceptance Scenarios**:
1. **Given** a requirement "invoice.view", **When** a user has "INVOICE.VIEW", **Then** access is granted.
2. **Given** a requirement "invoice.invoices.create", **When** a user has "invoice.*", **Then** access is granted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `RequirePermissionAttribute` MUST implement `IAuthorizationFilter`.
- **FR-002**: MUST validate permission format as `service.resource.action`. This validation MUST occur during registration, and the microservice MUST throw an exception on startup if an invalid format is detected (Fast-fail).
- **FR-003**: MUST read claims of type `permissions` from the current user.
- **FR-004**: `IAMRegistrationService` MUST be a `IHostedService`.
- **FR-005**: Registration MUST use `IHttpClientFactory` with Polly policies including Exponential Backoff retries and circuit-breaker, and MUST be strictly additive.
- **FR-006**: MUST provide `AddIAMClient` extension for `IServiceCollection`.
- **FR-007**: MUST provide `IAMTestHelpers` for fluent test authentication.
- **FR-008**: The system MUST perform enhanced audit logging when permissions marked as `IsCritical` are checked. This MUST be implemented as an enriched structured log event containing: `PrincipalId`, `ClientId`, `IP Address`, and `PermissionId`.

### Key Entities

- **PermissionRegistration**: Record with `PermissionId`, `ResourceType`, `Action`, `Description`, and `IsCritical` (used to trigger enhanced auditing).
- **RoleRegistration**: Record with `RoleId`, `RoleName`, `Description`, `Permissions[]`.

## Success Criteria

- [ ] `RequirePermissionAttribute` passes all authorization unit tests.
- [ ] `IAMRegistrationService` successfully registers with a mock IAM endpoint in integration tests.
- [ ] `AddIAMClient` correctly configures HttpClient with Resilience policies.
- [ ] `IAMTestHelpers` allows writing a passing authenticated test in < 5 lines of code.
- [ ] Zero build warnings in `Maliev.Aspire.ServiceDefaults`.
