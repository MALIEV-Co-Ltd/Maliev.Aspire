# Quickstart: Permission Enforcement and IAM Registration

## 1. Protecting Controller Actions

Add the `[RequirePermission]` attribute to your controllers or actions.

```csharp
[ApiController]
[Route("[controller]")]
public class InvoiceController : ControllerBase
{
    [HttpPost]
    [RequirePermission("invoice.invoices.create")]
    public IActionResult Create() => Ok();

    [HttpGet]
    [RequirePermission("invoice.invoices.view", "invoice.reporting.view")] // OR logic
    public IActionResult Get() => Ok();
}
```

## 2. Registering with IAM

Inherit from `IAMRegistrationService` and register it as a hosted service.

```csharp
public class MyIAMRegistration : IAMRegistrationService
{
    public MyIAMRegistration(IHttpClientFactory clientFactory, ILogger<MyIAMRegistration> logger) 
        : base(clientFactory, logger) { }

    protected override List<PermissionDefinition> GetPermissions() => new()
    {
        new("invoice.invoices.create", "Allow creating invoices"),
        new("invoice.invoices.view", "Allow viewing invoices")
    };

    protected override List<RoleDefinition> GetRoles() => new()
    {
        new("invoice-manager", new List<string> { "invoice.invoices.create", "invoice.invoices.view" })
    };
}

// In Program.cs
builder.Services.AddHostedService<MyIAMRegistration>();
```

## 3. Configuration

Ensure the IAM endpoint and service name are configured in `appsettings.json`.

```json
{
  "IAM": {
    "RegistrationEndpoint": "http://iam-service/api/v1/iam/registration",
    "ServiceName": "InvoiceService"
  }
}
```
