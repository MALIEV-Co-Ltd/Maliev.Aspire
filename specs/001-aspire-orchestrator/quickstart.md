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
