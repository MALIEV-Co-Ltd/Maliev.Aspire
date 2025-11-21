# Research & Decisions: Maliev.Aspire Orchestrator

**Status**: Complete.

## Findings

No significant research was required for this feature. The feature specification (`spec.md`) provides a clear and unambiguous set of requirements with a pre-defined technology stack (.NET Aspire). All technical decisions were directly derived from the specification.

The key decisions mandated by the specification are:
1.  **Orchestration Framework**: .NET Aspire
2.  **Containerized Infrastructure**: PostgreSQL, RabbitMQ, Redis
3.  **Configuration Strategy**: A two-layer approach using a git-ignored `sharedsecrets.json` for shared infrastructure credentials and .NET User Secrets for service-specific secrets.
4.  **Observability**: Handled by the built-in capabilities of the Aspire Dashboard and the `ServiceDefaults` project.
