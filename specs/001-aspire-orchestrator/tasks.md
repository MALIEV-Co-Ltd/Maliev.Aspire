---
description: "Task list for implementing the Maliev.Aspire Local Development Orchestrator"
---

# Tasks: Maliev.Aspire Local Development Orchestrator

**Input**: Design documents from `/specs/001-aspire-orchestrator/`
**Prerequisites**: spec.md, plan.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel
- **[Story]**: Maps to user stories from spec.md

## Phase 1: Setup (Project Scaffolding)

**Purpose**: Create the core .NET Aspire solution and projects as defined in `plan.md`.

- [ ] T001 Create the solution file `Maliev.Aspire.sln` in the root directory.
- [x] T002 Create the .NET Aspire AppHost project `Maliev.Aspire.AppHost/Maliev.Aspire.AppHost.csproj`.
- [x] T003 Create the .NET Aspire ServiceDefaults project `Maliev.Aspire.ServiceDefaults/Maliev.Aspire.ServiceDefaults.csproj`.
- [x] T004 Add the `Maliev.Aspire.AppHost` project to the `Maliev.Aspire.sln` solution file.
- [x] T005 Add the `Maliev.Aspire.ServiceDefaults` project to the `Maliev.Aspire.sln` solution file.
- [x] T006 Ensure the root `.gitignore` file contains entries for build artifacts (`bin/`, `obj/`).

---

## Phase 2: Foundational (Infrastructure & Shared Configuration)

**Purpose**: Define containerized infrastructure and the shared secret management mechanism. This phase directly addresses requirements from **User Story 2**.

- [x] T007 [P] In `Maliev.Aspire.AppHost/AppHost.cs`, add a container resource for `PostgreSQL`.
- [x] T008 [P] In `Maliev.Aspire.AppHost/AppHost.cs`, add a container resource for `RabbitMQ`.
- [x] T009 [P] In `Maliev.Aspire.AppHost/AppHost.cs`, add a container resource for `Redis`.
- [x] T010 Create a placeholder file `Maliev.Aspire.AppHost/sharedsecrets.json.template` with example keys for infrastructure connection strings.
- [x] T011 In `Maliev.Aspire.AppHost/AppHost.cs`, add code to load configuration from `sharedsecrets.json`.
- [x] T012 Add `sharedsecrets.json` to the root `.gitignore` file to prevent accidental commits.

---

## Phase 3: User Story 1 - Single-Command Environment Startup (Service Orchestration)

**Goal**: Orchestrate all 20 existing microservices for a single-command startup.

**Independent Test**: Run `dotnet run` from the AppHost project. All 20 services should start and appear as healthy in the Aspire Dashboard.

### Implementation for User Story 1

- [x] T013 [P] [US1] Add project reference for `Maliev.AuthService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T014 [P] [US1] Add project reference for `Maliev.CareerService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T015 [P] [US1] Add project reference for `Maliev.ChatbotService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T016 [P] [US1] Add project reference for `Maliev.ContactService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T017 [P] [US1] Add project reference for `Maliev.CountryService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T018 [P] [US1] Add project reference for `Maliev.CurrencyService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T019 [P] [US1] Add project reference for `Maliev.CustomerService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T020 [P] [US1] Add project reference for `Maliev.EmployeeService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T021 [P] [US1] Add project reference for `Maliev.InvoiceService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T022 [P] [US1] Add project reference for `Maliev.MaterialService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T023 [P] [US1] Add project reference for `Maliev.OrderService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T024 [P] [US1] Add project reference for `Maliev.PaymentService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T025 [P] [US1] Add project reference for `Maliev.PdfService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T026 [P] [US1] Add project reference for `Maliev.PredictionService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T027 [P] [US1] Add project reference for `Maliev.PurchaseOrderService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T028 [P] [US1] Add project reference for `Maliev.QuotationRequestService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T029 [P] [US1] Add project reference for `Maliev.QuotationService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T030 [P] [US1] Add project reference for `Maliev.ReceiptService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T031 [P] [US1] Add project reference for `Maliev.SupplierService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T032 [P] [US1] Add project reference for `Maliev.UploadService.Api` to `Maliev.Aspire.AppHost.csproj`.
- [x] T033 [US1] In `Maliev.Aspire.AppHost/AppHost.cs`, ensure all microservice resources are configured with `.WithReference()` to inject the infrastructure connection strings.

---

## Phase 4: User Story 4 - Standardized Observability

**Goal**: Ensure all microservices are correctly configured for standardized health checks and telemetry.

**Independent Test**: Call the `/health` endpoint on any orchestrated microservice and see a "Healthy" response. View the Aspire Dashboard to confirm logs and traces are present for all services.

### Implementation for User Story 4
- [x] T034 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.AuthService.Api.csproj`.
- [x] T035 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.CareerService.Api.csproj`.
- [x] T036 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.ChatbotService.Api.csproj`.
- [x] T037 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.ContactService.Api.csproj`.
- [x] T038 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.CountryService.Api.csproj`.
- [x] T039 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.CurrencyService.Api.csproj`.
- [x] T040 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.CustomerService.Api.csproj`.
- [x] T041 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.EmployeeService.Api.csproj`.
- [x] T042 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.InvoiceService.Api.csproj`.
- [x] T043 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.MaterialService.Api.csproj`.
- [x] T044 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.OrderService.Api.csproj`.
- [x] T045 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.PaymentService.Api.csproj`.
- [x] T046 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.PdfService.Api.csproj`.
- [x] T047 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.PredictionService.Api.csproj`.
- [x] T048 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.PurchaseOrderService.Api.csproj`.
- [x] T049 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.QuotationRequestService.Api.csproj`.
- [x] T050 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.QuotationService.Api.csproj`.
- [x] T051 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.ReceiptService.Api.csproj`.
- [x] T052 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.SupplierService.Api.csproj`.
- [x] T053 [P] [US4] Add a project reference to `Maliev.Aspire.ServiceDefaults` in `Maliev.UploadService.Api.csproj`.
- [x] T054 [US4] In each microservice's `Program.cs`, ensure `builder.AddServiceDefaults()` is called.

---

## Phase 5: Polish & Documentation

**Purpose**: Finalize the project with documentation and validation steps.

- [x] T055 Create a `README.md` in the root directory explaining how to set up and run the project, including instructions for creating `sharedsecrets.json`.
- [x] T056 Validate the entire solution by running `dotnet build` and confirming a successful build. (NOTE: Build succeeded with warnings for some microservices.)
- [x] T057 Add a `quickstart.md` document in `specs/001-aspire-orchestrator/` that describes the end-to-end validation test from T056.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** must be completed first.
- **Phase 2 (Foundational)** depends on Phase 1. It is a prerequisite for all other phases.
- **Phase 3 (US1)** and **Phase 4 (US4)** can be worked on in parallel after Phase 2 is complete. The tasks within them (marked [P]) are highly parallelizable.
- **Phase 5 (Polish)** should be done last.

## Implementation Strategy

### MVP First (User Story 1)

1.  Complete **Phase 1** and **Phase 2**.
2.  Complete all tasks in **Phase 3** to get all services orchestrated.
3.  At this point, the core requirement of single-command startup is met. This is the minimum viable product.

### Incremental Delivery

1.  After the MVP is complete, proceed to **Phase 4** to layer in the standardized observability.
2.  Finish with **Phase 5** for documentation and final validation.