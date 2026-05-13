# IAM Authorization Components

Shared IAM authorization components for the MALIEV microservices ecosystem.

## Components

### 1. RequirePermissionAttribute
Declarative permission-based authorization for controller actions.

```csharp
[HttpPost]
[RequirePermission("invoice.invoices.create")]
public IActionResult Create() => Ok();
```

- Supports OR logic for multiple permissions.
- Supports wildcards (e.g., `invoice.*`).
- Performs enhanced audit logging for critical actions (`IsCritical = true`).

### 2. IAMRegistrationService
Base class for services to register permissions and roles with IAM on startup.

```csharp
public class MyServiceRegistration : IAMRegistrationService
{
    // Implement GetPermissions() and GetPredefinedRoles()
}
```

### 3. AddIAMClient
Extension method to configure the resilient IAM HTTP client.

```csharp
builder.Services.AddIAMClient(builder.Configuration, "MyService");
```

The client attaches a service-account bearer token for trusted service-to-service calls.
Outside Development and Testing, service-account tokens require `Jwt:PrivateKey` and are signed with RS256.
The legacy `Jwt:SecurityKey` HS256 path is a local fallback only and is not accepted by production JWT validation.

## Configuration

Add the following to your `appsettings.json`:

```json
{
  "ExternalServices": {
    "IAM": {
      "BaseUrl": "http://iam-service",
      "ServiceName": "MyService",
      "Timeout": 5000,
      "RetryCount": 3
    }
  }
}
```

JWT requirements:

```json
{
  "Jwt": {
    "PublicKey": "<base64-rsa-public-pem>",
    "PrivateKey": "<base64-rsa-private-pem>",
    "Issuer": "https://api.maliev.com",
    "Audience": "https://api.maliev.com"
  }
}
```

Do not depend on `Jwt:SecurityKey` for staging or production token validation.
