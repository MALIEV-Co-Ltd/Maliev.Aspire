# Foundation Domain Test Specification

## 1. Scope
Validates the core infrastructure services that all other domains depend on:
- **IAM Service**: Identity management (Principals, Permissions).
- **Auth Service**: Token generation (JWT).
- **Registry Services**: Country and Currency validation.
- **Notification Service**: System-wide alerting.

## 2. Prerequisites
- **Seed Data**: System must be seeded with standard Countries (ISO-3166) and Currencies (ISO-4217).
- **Admin User**: A bootstrap admin account must exist to perform initial setup.

## 3. Test Scenarios

### 3.1. Identity & Access Management (IAM)
#### Scenario: Provision New User Principal
- **Goal**: Verify a new user identity can be created for downstream services.
- **Actor**: System Admin / Bootstrap Process.
- **Endpoint**: `POST /iam/v1/principals`
- **Payload**:
  ```json
  { "principalType": "user", "email": "test.user@maliev.com", "displayName": "Test User" }
  ```
- **Assertions**:
  - Response 201 Created with a valid `Guid` PrincipalId.
  - Query `GET /iam/v1/principals/{id}` returns the created record.

#### Scenario: Role Assignment
- **Goal**: Grant permissions to a principal.
- **Endpoint**: `POST /iam/v1/principals/{id}/roles`
- **Payload**: `{ "roleId": "roles.platform.user", "resourcePath": "*" }`
- **Assertions**:
  - Response 200 OK.
  - User permissions cache is invalidated/updated.

### 3.2. Authentication
#### Scenario: Login & Token Generation
- **Goal**: Obtain a valid JWT for API access.
- **Endpoint**: `POST /auth/v1/login`
- **Payload**: `{ "username": "test.user@maliev.com", "password": "..." }`
- **Assertions**:
  - Response contains `accessToken` (JWT) and `refreshToken`.
  - JWT contains expected claims (`sub`, `email`, `role`).
  - **Security Check**: Attempt login with wrong password -> 401 Unauthorized.

### 3.3. Registry (Reference Data)
#### Scenario: Country Validation
- **Goal**: Ensure services can validate country codes.
- **Endpoint**: `GET /country/v1/countries/iso2/US`
- **Assertions**:
  - Response 200 OK.
  - Payload contains "United States".
- **Negative Test**: `GET /country/v1/countries/iso2/XX` -> 404 Not Found.

### 3.4. Notifications
#### Scenario: System Alert Distribution
- **Goal**: Verify services can trigger notifications via Message Bus.
- **Action**: Publish `NotificationEvent` to RabbitMQ.
  - **Routing Key**: `notification.event.published`
  - **Payload**: `{ "type": "welcome_email", "targetUserId": "..." }`
- **Assertions**:
  - **Consumer**: `NotificationService` consumes the message (check logs/metrics).
  - **Outcome**: Mock email sender records a "sent" action.
