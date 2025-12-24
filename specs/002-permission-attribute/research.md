# Research: Permission Enforcement and IAM Registration

## Findings

### 1. IAM Registration Endpoint
- **Decision**: Define a standard HTTP contract for IAM registration.
- **Rationale**: A consistent contract allows all services to communicate with the central IAM service regardless of internal implementation.
- **Contract**:
  - `POST /api/v1/iam/registration`
  - Body:
    ```json
    {
      "serviceName": "string",
      "permissions": [
        { "name": "string", "description": "string" }
      ],
      "roles": [
        { "name": "string", "permissions": ["string"] }
      ]
    }
    ```
- **Alternatives considered**: gRPC (rejected for simpler HTTP/JSON integration in ServiceDefaults).

### 2. Wildcard Matching Pattern
- **Decision**: Use a segment-based matching approach (splitting by `.`) rather than Regular Expressions.
- **Rationale**: Segment-based matching is significantly faster and more predictable for hierarchical permissions like `invoice.create`.
- **Implementation**:
  ```csharp
  // Example logic
  bool Match(string requirement, string claim) {
      var reqParts = requirement.Split('.');
      var claimParts = claim.Split('.');
      // compare segments, handle *
  }
  ```
- **Alternatives considered**: Regex (rejected due to SC-004 performance targets).

### 3. Permission Format Validation
- **Decision**: Enforce `^[a-z0-9-]+(\.[a-z0-9-]+){2}$` for standard permissions and support `*` for claims.
- **Rationale**: Ensures consistency across the ecosystem (service.resource.action).
- **Alternatives considered**: Free-form strings (rejected to prevent ecosystem fragmentation).

### 4. RequirePermissionAttribute Implementation
- **Decision**: Implement as a `TypeFilterAttribute` that instantiates an `IAsyncAuthorizationFilter`.
- **Rationale**: Allows injection of dependencies (like `IPermissionMatcher` or `ILogger`) which a standard `Attribute` cannot do.
- **Alternatives considered**: `AuthorizeAttribute` with a custom `AuthorizationHandler` (rejected as it requires complex Policy setup for every unique permission string).
