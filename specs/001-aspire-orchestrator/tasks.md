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
---

## Phase 6: CI/CD Integration (ServiceDefaults as NuGet Package)

**Purpose**: Enable microservices in separate repositories to reference ServiceDefaults via NuGet package instead of project reference.

**Independent Test**: Push a commit to any microservice repo (e.g., Maliev.AuthService) and verify CI pipeline passes.

### ServiceDefaults Package Publishing (Maliev.Aspire repo)

- [x] T058 [P] [US5] Update `Maliev.Aspire.ServiceDefaults.csproj` to be packable with NuGet metadata.
- [x] T059 [P] [US5] Add multi-targeting support for net9.0 and net10.0 in ServiceDefaults.
- [x] T060 [US5] Create `.github/workflows/publish-nuget.yml` to publish package to GitHub Packages on push to main.
- [x] T061 [US5] Create `nuget.config` in Maliev.Aspire repo root.

### Microservice Updates (repeat for each microservice)

For **Maliev.AuthService** (reference implementation):

- [x] T062 [US5] Replace `ProjectReference` to ServiceDefaults with `PackageReference` in `Maliev.AuthService.Api.csproj`.
- [x] T063 [US5] Create `nuget.config` in Maliev.AuthService repo with GitHub Packages source.
- [x] T064 [US5] Update `ci-develop.yml` to pass `NUGET_USERNAME` and `NUGET_PASSWORD` env vars to restore.
- [x] T065 [US5] Update `ci-staging.yml` with NuGet authentication.
- [x] T066 [US5] Update `ci-main.yml` with NuGet authentication.
- [x] T067 [US5] Update `Dockerfile` to copy `nuget.config` and use BuildKit secrets for authentication.
- [x] T068 [US5] Update CI workflows to pass secrets to Docker build using `--secret` flag.

### Remaining Microservices (TODO)

For each remaining microservice, apply the same changes as T062-T068:

- [ ] T069 [P] [US5] Update Maliev.CareerService for GitHub Packages integration.
- [ ] T070 [P] [US5] Update Maliev.ChatbotService for GitHub Packages integration.
- [ ] T071 [P] [US5] Update Maliev.ContactService for GitHub Packages integration.
- [ ] T072 [P] [US5] Update Maliev.CountryService for GitHub Packages integration.
- [ ] T073 [P] [US5] Update Maliev.CurrencyService for GitHub Packages integration.
- [ ] T074 [P] [US5] Update Maliev.CustomerService for GitHub Packages integration.
- [ ] T075 [P] [US5] Update Maliev.EmployeeService for GitHub Packages integration.
- [ ] T076 [P] [US5] Update Maliev.InvoiceService for GitHub Packages integration.
- [ ] T077 [P] [US5] Update Maliev.MaterialService for GitHub Packages integration.
- [ ] T078 [P] [US5] Update Maliev.OrderService for GitHub Packages integration.
- [ ] T079 [P] [US5] Update Maliev.PaymentService for GitHub Packages integration.
- [ ] T080 [P] [US5] Update Maliev.PdfService for GitHub Packages integration.
- [ ] T081 [P] [US5] Update Maliev.PredictionService for GitHub Packages integration.
- [ ] T082 [P] [US5] Update Maliev.PurchaseOrderService for GitHub Packages integration.
- [ ] T083 [P] [US5] Update Maliev.QuotationRequestService for GitHub Packages integration.
- [ ] T084 [P] [US5] Update Maliev.QuotationService for GitHub Packages integration.
- [ ] T085 [P] [US5] Update Maliev.ReceiptService for GitHub Packages integration.
- [ ] T086 [P] [US5] Update Maliev.SupplierService for GitHub Packages integration.
- [ ] T087 [P] [US5] Update Maliev.UploadService for GitHub Packages integration.

---

## Implementation Notes for Phase 6

### Changes Required per Microservice

1. **nuget.config** (create in repo root):
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/MALIEV-Co-Ltd/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="%NUGET_USERNAME%" />
      <add key="ClearTextPassword" value="%NUGET_PASSWORD%" />
    </github>
  </packageSourceCredentials>
</configuration>
```

2. **csproj update** (replace ProjectReference):
```xml
<!-- Remove -->
<ProjectReference Include="..\..\Maliev.Aspire\Maliev.Aspire.ServiceDefaults\Maliev.Aspire.ServiceDefaults.csproj" />

<!-- Add -->
<PackageReference Include="Maliev.Aspire.ServiceDefaults" Version="1.0.*" />
```

3. **CI workflow update** (add env vars to restore step):
```yaml
- name: Restore dependencies
  run: dotnet restore
  env:
    NUGET_USERNAME: ${{ github.actor }}
    NUGET_PASSWORD: ${{ secrets.GITOPS_PAT }}
```

4. **Dockerfile update** (use BuildKit secrets):
```dockerfile
# syntax=docker/dockerfile:1.4
# ... 
RUN --mount=type=secret,id=nuget_username \
    --mount=type=secret,id=nuget_password \
    NUGET_USERNAME=$(cat /run/secrets/nuget_username) \
    NUGET_PASSWORD=$(cat /run/secrets/nuget_password) \
    dotnet restore "./Project.csproj"
```

5. **Docker build command** (in CI workflow):
```yaml
NUGET_USERNAME=${{ github.actor }} NUGET_PASSWORD=${{ secrets.GITOPS_PAT }} docker build \
  --secret id=nuget_username,env=NUGET_USERNAME \
  --secret id=nuget_password,env=NUGET_PASSWORD \
  -t image:tag -f Dockerfile .
```

### Prerequisites

- `GITOPS_PAT` secret must have `read:packages` scope
- ServiceDefaults package must be published before microservice CI can succeed
