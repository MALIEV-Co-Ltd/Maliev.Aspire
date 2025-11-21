# Maliev.Aspire Orchestrator

This project uses .NET Aspire to orchestrate the local development environment for the Maliev microservices monorepo. It provides a single command to launch all services and their required infrastructure.

## Prerequisites

1.  .NET 8 SDK or later.
2.  Docker Desktop (or a compatible container runtime) installed and running.
3.  The Maliev monorepo cloned to your local machine.

## Getting Started

### 1. Configure Shared Secrets

Before running the project for the first time, you must provide the shared secrets for the infrastructure components.

1.  Navigate to the `Maliev.Aspire/Maliev.Aspire.AppHost/` directory.
2.  Create a new file named `sharedsecrets.json`.
3.  Populate `sharedsecrets.json` with the necessary connection strings. You can use the `sharedsecrets.json.template` file as a reference.

**Example `sharedsecrets.json` content:**
```json
{
  "ConnectionStrings": {
    "postgres": "Host=localhost;Port=5432;Username=user;Password=password",
    "rabbitmq": "amqp://guest:guest@localhost:5672",
    "redis": "localhost:6379"
  }
}
```

### 2. Run the Environment

1.  Open a terminal.
2.  Navigate to the `Maliev.Aspire/Maliev.Aspire.AppHost/` directory.
3.  Run the following command:
    ```bash
    dotnet run
    ```

### 3. Validate the Setup

1.  **Observe Console Output**: The console will show the startup process, including pulling container images and launching services. Note the URL provided for the **Aspire Dashboard**.
2.  **Access Aspire Dashboard**: Open the dashboard URL in your browser (e.g., `http://localhost:15463`).
3.  **Verify Resources**: On the "Resources" page of the dashboard, you should see all enabled microservices and the 3 infrastructure containers (`postgres`, `rabbitmq`, `redis`).
4.  **Check Health Status**: All running resources should eventually transition to a **"Healthy"** state.

## Managing Services

To add or remove services from the orchestration:
1.  Edit `Maliev.Aspire/Maliev.Aspire.AppHost/Maliev.Aspire.AppHost.csproj` to add or remove the `<ProjectReference>`.
2.  Edit `Maliev.Aspire/Maliev.Aspire.AppHost/AppHost.cs` to add or remove the corresponding `builder.AddProject<T>()` line.

Unimplemented services are currently commented out in these files. To enable them, simply uncomment the relevant lines.