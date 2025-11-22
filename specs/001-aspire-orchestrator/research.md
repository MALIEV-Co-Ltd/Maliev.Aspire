# Research & Decisions: Maliev.Aspire Orchestrator

**Status**: Complete.

## Findings

No significant research was required for this feature. The feature specification (`spec.md`) provides a clear and unambiguous set of requirements with a pre-defined technology stack (.NET Aspire). All technical decisions were directly derived from the specification.

The key decisions mandated by the specification are:
1.  **Orchestration Framework**: .NET Aspire
2.  **Containerized Infrastructure**: PostgreSQL, RabbitMQ, Redis
3.  **Configuration Strategy**: A two-layer approach using a git-ignored `sharedsecrets.json` for shared infrastructure credentials and .NET User Secrets for service-specific secrets.
4.  **Observability**: Handled by the built-in capabilities of the Aspire Dashboard and the `ServiceDefaults` project.

---

## Session 2025-11-21: CI/CD Integration Research

### Problem

When microservices are in separate repositories, CI builds fail because they cannot resolve the ServiceDefaults project reference:

```
error NU1101: Unable to find package Maliev.Aspire.ServiceDefaults. 
No packages exist with this id in source(s): nuget.org
```

### Options Evaluated

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| **Git Submodules** | No package management | Complex, requires submodule init | Rejected |
| **Conditional References** | Works locally and CI | Adds complexity to csproj | Rejected |
| **NuGet Package (GitHub Packages)** | Clean separation, versioned | Requires package publishing | **Selected** |
| **NuGet Package (Azure Artifacts)** | Enterprise features | Different platform than GitHub | Rejected |

### Selected Solution: GitHub Packages

**Rationale**: 
- Already using GitHub for source control
- Free for private packages within organization
- Integrates with existing `GITOPS_PAT` for cross-repo access
- Supports standard NuGet tooling

### Key Findings

1. **Cross-Repository Authentication**
   - `GITHUB_TOKEN` scope is limited to the current repository
   - Cannot access packages from `Maliev.Aspire` when running in `Maliev.AuthService` CI
   - Solution: Use `GITOPS_PAT` which has organization-wide access + `read:packages` scope

2. **Docker Build Authentication**
   - Initial approach: Pass credentials via `--build-arg NUGET_PASSWORD=...`
   - Problem: Docker warns "Do not use ARG for sensitive data" (visible in `docker history`)
   - Solution: Use Docker BuildKit secrets with `--mount=type=secret`

3. **Package Versioning**
   - Auto-increment: `1.0.${{ github.run_number }}`
   - Release tags: Strip `v` prefix from tag name
   - Manual override: `workflow_dispatch` with version input

### Implementation Reference

See `Maliev.AuthService` repository for reference implementation:
- `nuget.config` - GitHub Packages source configuration
- `Maliev.AuthService.Api.csproj` - PackageReference usage
- `.github/workflows/ci-develop.yml` - NuGet authentication in CI
- `Maliev.AuthService.Api/Dockerfile` - BuildKit secrets usage
