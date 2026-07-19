# HR Domain Test Specification

## 1. Scope
Validates the human resources workflows managed by:
- **Employee Service**: Core profile management.
- **Leave Service**: Time-off requests and approvals.

## 2. Test Scenarios

### 2.1. Employee Lifecycle
#### Scenario: Hire New Employee (Happy Path)
- **Goal**: Onboard a new employee with all required associations.
- **Actor**: HR Manager.
- **Endpoint**: `POST /employee/v1/hr/employees`
- **Payload**:
  ```json
  {
    "firstName": "John", "lastName": "Doe",
    "email": "john.doe@maliev.com",
    "departmentId": "{ValidDepartmentId}",
    "managerId": "{ValidManagerId}"
  }
  ```
- **Assertions**:
  - Response 201 Created with `EmployeeId`.
  - **Integration**:
    - IAM Identity created (check `PrincipalId`).
    - Audit Log entry created.
    - User added to "Employees" group in Auth.

#### Scenario: Business Rule - Circular Reporting
- **Goal**: Prevent invalid hierarchy chains.
- **Action**: Update Employee A to report to Employee B, where B already reports to A.
- **Endpoint**: `PUT /employee/v1/hr/employees/{id}/manager`
- **Assertions**:
  - Response 400 Bad Request.
  - Error message cites "Circular reference detected".

### 2.2. Leave Management
#### Scenario: Submit & Approve Leave
- **Goal**: End-to-end leave workflow.
- **Precondition**: Employee has > 0 leave balance.
- **Steps**:
  1. **Employee**: `POST /leave/v1/leaverequests/{id}` (Type: Annual, Days: 3).
     - Assert: 201 Created, Status `Pending`.
  2. **Manager**: `POST /leave/v1/leaverequests/{requestId}/decision` (Approve).
     - Assert: 200 OK.
  3. **Verify**:
     - Request Status = `Approved`.
     - Employee Leave Balance decreased by 3.
