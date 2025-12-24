---
description: "Task list for Permission Enforcement and IAM Registration"
---

# Tasks: Permission Enforcement and IAM Registration

**Input**: Design documents from `/specs/002-permission-attribute/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create folder structure for `Authorization`, `IAM`, and `Testing` in `Maliev.Aspire.ServiceDefaults/` and `Maliev.Aspire.Tests/`
- [x] T002 [P] Verify `Microsoft.AspNetCore.Authorization` and `Microsoft.Extensions.Http.Polly` dependencies in `Maliev.Aspire.ServiceDefaults/Maliev.Aspire.ServiceDefaults.csproj`
- [x] T003 [P] Configure zero-warnings policy in `Maliev.Aspire.ServiceDefaults/Maliev.Aspire.ServiceDefaults.csproj`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

- [x] T004 [P] Define `PermissionRegistration` record in `Maliev.Aspire.ServiceDefaults/IAM/PermissionRegistration.cs`
- [x] T005 [P] Define `RoleRegistration` record in `Maliev.Aspire.ServiceDefaults/IAM/RoleRegistration.cs`
- [x] T006 [P] Define `IServicePermissions` interface in `Maliev.Aspire.ServiceDefaults/IAM/IServicePermissions.cs`
- [x] T007 Implement internal `PermissionMatcher` utility for segment-based logic in `Maliev.Aspire.ServiceDefaults/IAM/PermissionMatcher.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Secure Controller Action (Priority: P1) 🎯 MVP

**Goal**: Implement declarative permission-based authorization for controller actions

**Independent Test**: Decorate a test controller with `[RequirePermission("test.resource.action")]` and verify auth challenges/forbids.

### Tests for User Story 1

- [x] T008 [P] [US1] Create unit tests for attribute authorization logic in `Maliev.Aspire.Tests/Authorization/RequirePermissionAttributeTests.cs`
- [x] T009 [P] [US1] Create unit tests for permission format validation in `Maliev.Aspire.Tests/Authorization/PermissionFormatTests.cs`
- [x] T031 [US1] Create unit test to verify enhanced audit logging fields for critical permissions in `Maliev.Aspire.Tests/Authorization/RequirePermissionAttributeTests.cs`

### Implementation for User Story 1

- [x] T010 [US1] Implement `RequirePermissionAttribute` as `IAuthorizationFilter` in `Maliev.Aspire.ServiceDefaults/Authorization/RequirePermissionAttribute.cs`
- [x] T011 [US1] Implement "permissions" claim extraction from JWT in `RequirePermissionAttribute.cs`
- [x] T012 [US1] Add format validation (`service.resource.action`) to attribute constructor in `RequirePermissionAttribute.cs`
- [x] T013 [US1] Implement enhanced audit logging for `IsCritical` permissions in `RequirePermissionAttribute.cs`

**Checkpoint**: User Story 1 is fully functional and testable independently.

---

## Phase 4: User Story 2 - Automated IAM Registration (Priority: P2)

**Goal**: Enable microservices to register their capabilities with IAM on startup

**Independent Test**: Verify POST requests to registration endpoints during service startup in an integration test.

### Tests for User Story 2

- [x] T014 [P] [US2] Setup Testcontainers to host a mock IAM container in `Maliev.Aspire.Tests/IAM/IAMRegistrationServiceTests.cs`
- [x] T015 [US2] Write failing integration test for registration flow using Testcontainers in `Maliev.Aspire.Tests/IAM/IAMRegistrationServiceTests.cs`

### Implementation for User Story 2

- [x] T016 [US2] Implement `IAMRegistrationService` base class as `IHostedService` in `Maliev.Aspire.ServiceDefaults/IAM/IAMRegistrationService.cs`
- [x] T017 [US2] Implement registration POST logic using `IHttpClientFactory` in `IAMRegistrationService.cs`
- [x] T018 [US2] Add fail-open logic (log error, continue startup) in `IAMRegistrationService.cs`
- [x] T019 [US2] Implement strictly additive registration (no deletion logic) in `IAMRegistrationService.cs`

**Checkpoint**: User Story 2 enables automated ecosystem registration.

---

## Phase 5: User Story 3 - Case-Insensitive & Wildcard Matching (Priority: P3)

**Goal**: Support hierarchical and flexible permission matching

**Independent Test**: Use `invoice.*` claim to access `invoice.invoices.create` protected endpoint.

### Tests for User Story 3

- [x] T020 [P] [US3] Add wildcard matching test cases to `Maliev.Aspire.Tests/Authorization/RequirePermissionAttributeTests.cs`
- [x] T021 [P] [US3] Add case-insensitivity test cases to `Maliev.Aspire.Tests/Authorization/RequirePermissionAttributeTests.cs`

### Implementation for User Story 3

- [x] T022 [US3] Update `PermissionMatcher.cs` to handle `*` segments and ordinal ignore-case comparison
- [x] T023 [US3] Integrate updated matcher into `RequirePermissionAttribute.cs`

**Checkpoint**: Advanced matching logic is fully integrated and verified.

---

## Phase 6: Infrastructure & Helpers

**Purpose**: Improvements for usability and testing

- [x] T024 [P] Implement `AddIAMClient` extension with Polly retry/backoff in `Maliev.Aspire.ServiceDefaults/Extensions.IAM.cs`
- [x] T025 [P] Implement `IAMTestHelpers` for fluent JWT creation in `Maliev.Aspire.ServiceDefaults/Testing/IAMTestHelpers.cs`
- [x] T026 [P] Create unit tests for test helpers in `Maliev.Aspire.Tests/Testing/IAMTestHelpersTests.cs`

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation and validation

- [x] T027 [P] Generate XML documentation comments for all new public classes and methods
- [x] T028 [P] Create `README.md` and `MIGRATION_GUIDE.md` in `Maliev.Aspire.ServiceDefaults/IAM/`
- [x] T029 Run `quickstart.md` validation using a sample implementation in a test project
- [x] T030 [P] Ensure all code passes `dotnet format` and emits zero warnings

---

## Dependencies & Execution Order

### Phase Dependencies

1. **Setup (Phase 1)**: Start immediately.
2. **Foundational (Phase 2)**: Depends on Phase 1.
3. **User Story 1 (Phase 3)**: Depends on Phase 2.
4. **User Story 2 (Phase 4)**: Depends on Phase 2.
5. **User Story 3 (Phase 5)**: Depends on US1 completion.
6. **Infrastructure (Phase 6)**: Depends on US2 completion.
7. **Polish (Phase 7)**: Depends on all previous phases.

### Parallel Opportunities

- T002, T003 can run with T001.
- T004, T005, T006 can run in parallel.
- US1 and US2 implementation can technically run in parallel once Foundation is done.
- All tasks marked with [P] within a phase can run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 & 2.
2. Implement User Story 1 (Basic exact matching).
3. Validate with Unit Tests.

### Incremental Delivery

1. Foundation -> Core Attribute (MVP) -> Registration Service -> Wildcard Matching -> Helpers -> Documentation.
2. Each phase delivers a testable increment to `ServiceDefaults`.