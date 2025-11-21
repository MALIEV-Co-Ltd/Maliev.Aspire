# Quickstart: Maliev.Aspire Orchestrator

This guide describes how to run the fully configured Maliev local development environment and validate that it is working correctly.

## Prerequisites

1.  .NET 8 SDK or later.
2.  Docker Desktop (or a compatible container runtime) installed and running.
3.  The Maliev monorepo cloned to your local machine.

## 1. Initial Setup

Before running the project for the first time, you must provide the shared secrets.

1.  Navigate to the `Maliev.Aspire/Maliev.Aspire.AppHost/` directory.
2.  Create a new file named `sharedsecrets.json`.
3.  Populate `sharedsecrets.json` with the necessary connection strings for the infrastructure components. Use the `sharedsecrets.json.template` file (if available) as a reference.

    **Example `sharedsecrets.json` content:**
    ```json
    {
      "ConnectionStrings": {
        "postgres": "Host=localhost;Port=5432;Username=user;Password=password;",
        "rabbitmq": "amqp://guest:guest@localhost:5672",
        "redis": "localhost:6379"
      }
    }
    ```

## 2. Run the Environment

1.  Open a terminal.
2.  Navigate to the `Maliev.Aspire/Maliev.Aspire.AppHost/` directory.
3.  Run the following command:
    ```bash
    dotnet run
    ```

## 3. Validation

1.  **Observe Console Output**: The console will show the startup process. This includes pulling container images (on the first run) and launching all 20 microservices. Note the URL provided for the **Aspire Dashboard**.
2.  **Access Aspire Dashboard**: Open the dashboard URL in your browser (e.g., `http://localhost:15463`).
3.  **Verify Resources**: On the "Resources" page of the dashboard, you should see all 20 microservices and the 3 infrastructure containers (`postgres`, `rabbitmq`, `redis`) listed.
4.  **Check Health Status**: Allow a few moments for all services to initialize. All resources should eventually transition to a **"Healthy"** state.
5.  **View Logs and Traces**: Click on any service in the dashboard to view its logs in real-time. If you trigger requests that span multiple services, you can view the distributed traces on the "Traces" page.

---

## CI/CD Setup for Microservices

This section describes how to configure a microservice repository to use ServiceDefaults from GitHub Packages.

### Prerequisites

1. The `Maliev.Aspire.ServiceDefaults` package must be published to GitHub Packages (done via Aspire repo CI).
2. A Personal Access Token (PAT) with `read:packages` scope must be available as `GITOPS_PAT` secret.

### Step 1: Create nuget.config

Create `nuget.config` in the microservice repository root:

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

### Step 2: Update Project Reference

In your `.csproj` file, replace the project reference:

```xml
<!-- Remove this -->
<ProjectReference Include="..\..\Maliev.Aspire\Maliev.Aspire.ServiceDefaults\Maliev.Aspire.ServiceDefaults.csproj" />

<!-- Add this -->
<PackageReference Include="Maliev.Aspire.ServiceDefaults" Version="1.0.*" />
```

### Step 3: Update CI Workflows

Add environment variables to the restore step in all CI workflows:

```yaml
- name: Restore dependencies
  run: dotnet restore YourService.sln
  env:
    NUGET_USERNAME: ${{ github.actor }}
    NUGET_PASSWORD: ${{ secrets.GITOPS_PAT }}
```

### Step 4: Update Dockerfile

Add the BuildKit syntax directive and use secret mounts:

```dockerfile
# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy nuget.config for package source
COPY ["nuget.config", "."]
COPY ["YourService.Api/YourService.Api.csproj", "YourService.Api/"]

# Restore with BuildKit secrets
RUN --mount=type=secret,id=nuget_username \
    --mount=type=secret,id=nuget_password \
    NUGET_USERNAME=$(cat /run/secrets/nuget_username) \
    NUGET_PASSWORD=$(cat /run/secrets/nuget_password) \
    dotnet restore "./YourService.Api/YourService.Api.csproj"
```

### Step 5: Update Docker Build Command in CI

Update the docker build command in your CI workflow:

```yaml
- name: Build and push Docker image
  run: |
    NUGET_USERNAME=${{ github.actor }} NUGET_PASSWORD=${{ secrets.GITOPS_PAT }} \
    docker build \
      --secret id=nuget_username,env=NUGET_USERNAME \
      --secret id=nuget_password,env=NUGET_PASSWORD \
      -t your-image:tag \
      -f YourService.Api/Dockerfile .
```

### Local Development

For local development, set environment variables before running `dotnet restore`:

**Windows PowerShell:**
```powershell
$env:NUGET_USERNAME = "your-github-username"
$env:NUGET_PASSWORD = "your-personal-access-token"
dotnet restore
```

**Linux/macOS:**
```bash
export NUGET_USERNAME="your-github-username"
export NUGET_PASSWORD="your-personal-access-token"
dotnet restore
```

Or configure credentials permanently:
```bash
dotnet nuget update source github \
  --username YOUR_USERNAME \
  --password YOUR_PAT \
  --store-password-in-clear-text
```
