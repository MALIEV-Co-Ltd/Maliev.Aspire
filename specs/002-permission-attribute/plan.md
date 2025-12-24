# Implementation Plan: Permission Enforcement and IAM Registration

**Branch**: `002-permission-attribute` | **Date**: 2025-12-21 | **Spec**: [specs/002-permission-attribute/spec.md](spec.md)
**Input**: Feature specification and `servicedefaults-plan.md`

## Summary

Implement a shared IAM authorization system within `Maliev.Aspire.ServiceDefaults`. This includes a `RequirePermissionAttribute` for fine-grained controller access, an `IAMRegistrationService` base class for automated microservice registration, extension methods for robust IAM client configuration (Polly), and specialized test helpers for authentication testing.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: 
- `Microsoft.AspNetCore.Authorization`
- `Microsoft.AspNetCore.Mvc.Core`
- `Microsoft.Extensions.Http.Polly`
- `System.IdentityModel.Tokens.Jwt`
**Storage**: N/A (External IAM Service)  
**Testing**: xUnit, Testcontainers (Mandatory for IAM registration integration tests), `Microsoft.AspNetCore.Mvc.Testing`  
**Target Platform**: Linux/Docker  
**Project Type**: Shared Library (`Maliev.Aspire.ServiceDefaults`)  
**Performance Goals**: < 5ms overhead per request for permission checks  
**Constraints**: Zero Warnings, No AutoMapper, No FluentValidation, No FluentAssertions  

## Constitution Check

- [x] **Service Autonomy**: Components are built into ServiceDefaults for consumption by independent microservices.
- [x] **Explicit Contracts**: Defined `PermissionRegistration` and `RoleRegistration` records.
- [x] **Test-First Development**: Unit tests for attribute and registration logic will be implemented first.
- [x] **Real Infrastructure Testing**: Testcontainers used for integration testing.
- [x] **Auditability & Observability**: Structured logging for registration and authorization failures.
- [x] **Security & Compliance**: Implements JWT-based permission enforcement.
- [x] **Zero Warnings Policy**: Build MUST emit zero warnings.
- [x] **.NET Aspire Integration**: Native integration into `AddServiceDefaults()`.

## Project Structure

### Documentation (this feature)

```text
specs/002-permission-attribute/
├── plan.md              # This file
├── research.md          # Design decisions
├── data-model.md        # Registration models
├── quickstart.md        # Usage examples
├── contracts/           # API schemas
└── tasks.md             # Task breakdown
```

### Source Code

```text
Maliev.Aspire.ServiceDefaults/
├── Authorization/
│   └── RequirePermissionAttribute.cs
├── IAM/
│   ├── IAMRegistrationService.cs
│   ├── PermissionRegistration.cs
│   ├── RoleRegistration.cs
│   └── IServicePermissions.cs
├── Testing/
│   └── IAMTestHelpers.cs
└── Extensions.IAM.cs

Maliev.Aspire.Tests/
├── Authorization/
│   └── RequirePermissionAttributeTests.cs
├── IAM/
│   └── IAMRegistrationServiceTests.cs
├── Testing/
│   └── IAMTestHelpersTests.cs
└── Integration/
    └── IAMClientConfigurationTests.cs
```

**Structure Decision**: Components distributed across `Authorization`, `IAM`, and `Testing` namespaces within `Maliev.Aspire.ServiceDefaults`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
