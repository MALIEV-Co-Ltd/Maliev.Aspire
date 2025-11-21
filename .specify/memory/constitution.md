<!--
SYNC IMPACT REPORT

- Version Change: None -> 1.0.0
- Rationale: Initial creation of the project constitution based on the .NET Aspire orchestrator feature.
- Added Principles:
  - I. Unified Local Environment
  - II. Standardized Orchestration
  - III. Declarative Infrastructure
  - IV. Secure & Layered Configuration
  - V. Seamless Service Integration
- Removed Sections:
  - SECTION_2
  - SECTION_3
- Templates Requiring Updates:
  - [✅] .specify/templates/plan-template.md (No content change needed, but the 'Constitution Check' section will now be interpreted against these new principles).
  - [✅] .specify/templates/spec-template.md (No changes needed).
  - [✅] .specify/templates/tasks-template.md (No changes needed).
  - [✅] .specify/templates/agent-file-template.md (No changes needed).
  - [✅] .specify/templates/checklist-template.md (No changes needed).
- Follow-up TODOs: None.
-->
# Maliev.Aspire Constitution

## Core Principles

### I. Unified Local Environment
The project MUST provide a single entry point (`dotnet run` in the `AppHost`) for launching the entire microservices stack locally, including all backend services and required infrastructure. This environment SHOULD mirror the composition of deployed environments as closely as is practical to reduce setup friction for developers, ensure consistency, and catch integration issues early.

### II. Standardized Orchestration
The project MUST use .NET Aspire as the exclusive tool for orchestrating the local development environment. All services and infrastructure resources MUST be registered and managed as resources within the Aspire `AppHost`. This leverages a consistent, Microsoft-supported framework for service discovery, configuration, and lifecycle management during local development.

### III. Declarative Infrastructure
All backing infrastructure services (e.g., PostgreSQL, RabbitMQ, Redis) required for local development MUST be declared as version-controlled container resources within the Aspire `AppHost`. Manual setup of infrastructure is forbidden to automate the setup of dependencies, ensuring every developer works with identical infrastructure versions and configurations.

### IV. Secure & Layered Configuration
Secrets and configuration MUST be managed in a layered approach. Shared infrastructure connection strings MUST be loaded from a git-ignored `sharedsecrets.json` file. Service-specific secrets MUST use the standard .NET User Secrets mechanism. Hardcoding secrets, connection strings, or environment-specific URLs in source code is strictly forbidden to prevent accidental credential leakage.

### V. Seamless Service Integration
The orchestrator MUST be designed to easily incorporate new microservices into the local development environment. Adding a new service SHOULD only require adding a project reference and the corresponding resource definition in the `AppHost` to ensure the development environment can scale with the monorepo.

## Governance
This constitution is the single source of truth for project-level architectural and development standards. All feature specifications, implementation plans, and code contributions must be validated against its principles. Amendments to this constitution require a documented proposal and team consensus, and must result in a MINOR or MAJOR version bump. All dependent templates and documentation must be updated in sync with any amendments.

**Version**: 1.0.0 | **Ratified**: 2024-11-21 | **Last Amended**: 2024-11-21