# Data Model: Permission Enforcement and IAM Registration

## Entities

### PermissionRegistration (Record)
Defines a single permission capability.

| Field | Type | Description |
|-------|------|-------------|
| PermissionId | String | Unique identifier (e.g., `invoice.invoices.create`) |
| ResourceType | String | The entity type (e.g., `invoices`) |
| Action | String | The operation (e.g., `create`) |
| Description | String | Human-readable explanation |
| IsCritical | Boolean | Flag for high-security operations |

### RoleRegistration (Record)
Defines a template role for the service.

| Field | Type | Description |
|-------|------|-------------|
| RoleId | String | Unique identifier (e.g., `invoice-admin`) |
| RoleName | String | Display name |
| Description | String | Human-readable explanation |
| Permissions | String[] | Array of PermissionIds assigned to this role |

### JWT Claims
The system expects permissions to be delivered in the `permissions` claim.

- **Claim Type**: `permissions`
- **Matching**: Case-insensitive, supports wildcard (`*`).

## Validation Rules

- **Permission Format**: Must follow `service.resource.action` pattern (checked in `RequirePermissionAttribute` constructor).
- **Matching Logic**: Any match (OR logic) if multiple permissions are specified in the attribute.