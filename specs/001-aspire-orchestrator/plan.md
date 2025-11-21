# Implementation Plan: Maliev.Aspire Local Development Orchestrator

**Branch**: `001-aspire-orchestrator` | **Date**: 2024-11-21 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/001-aspire-orchestrator/spec.md`

## Summary

This plan details the implementation of a .NET Aspire solution to serve as a local development orchestrator for the Maliev microservices monorepo. It will automate the startup of all 20 microservices and their containerized dependencies (PostgreSQL, RabbitMQ, Redis) with a single command, manage shared configuration, and provide standardized observability.

## Technical Context

**Language/Version**: C# / .NET 8.0
**Primary Dependencies**: .NET Aspire, Docker
**Storage**: PostgreSQL, RabbitMQ, Redis (all containerized)
**Testing**: Manual validation via Aspire Dashboard and service endpoints
**Target Platform**: Local developer machine (Windows, macOS, or Linux with Docker)
**Project Type**: Orchestrator for an existing microservices monorepo
**Performance Goals**: Start the entire environment from a cold state in under 5 minutes (excluding one-time container image downloads).
**Constraints**: The solution must not require any manual container or service startup outside of the single `dotnet run` command.
**Scale/Scope**: The solution will orchestrate 20 existing microservices and 3 infrastructure services.

## Constitution Check

*GATE: Must pass before implementation.*

- **I. Unified Local Environment**: Pass. The plan mandates a single `dotnet run` entry point via the AppHost.
- **II. Standardized Orchestration**: Pass. The plan exclusively uses .NET Aspire for all orchestration.
- **III. Declarative Infrastructure**: Pass. All infrastructure is defined as code within the AppHost.
- **IV. Secure & Layered Configuration**: Pass. The plan specifies the `sharedsecrets.json` and .NET User Secrets strategy.
- **V. Seamless Service Integration**: Pass. The plan's structure of adding project references is modular and extensible.

**Result**: ✅ The plan is fully compliant with the project constitution.

## Project Structure

### Documentation (this feature)

```text
specs/001-aspire-orchestrator/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Not applicable for this feature
└── tasks.md             # Generated from this plan
```

### Source Code (repository root)

```text
Maliev.Aspire/
├── Maliev.Aspire.AppHost/      # The primary .NET Aspire orchestration project.
│   ├── Program.cs            # Main entry point for defining resources.
│   └── sharedsecrets.json    # (Git-ignored) Contains shared connection strings.
├── Maliev.Aspire.ServiceDefaults/ # A shared library for common service configurations.
└── Maliev.Aspire.sln           # The solution file containing the Aspire projects.
```

**Structure Decision**: The project structure is dictated by the standard .NET Aspire template, which includes an `AppHost` project for orchestration and a `ServiceDefaults` class library for common concerns like logging and health checks. This is the correct and intended structure for this feature.

## Complexity Tracking

No constitutional violations were identified that require justification.