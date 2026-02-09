# Supply Chain Domain Test Specification

## 1. Scope
Validates procurement workflows involving:
- **Supplier Service**: Vendor management.
- **Material Service**: Inventory items.
- **Purchase Order Service**: Buying goods.

## 2. Test Scenarios

### 2.1. Supplier & Material Setup
#### Scenario: Onboard Supplier
- **Endpoint**: `POST /supplier/v1/suppliers`
- **Payload**: `{ "companyName": "TechSteel Inc", "taxId": "US-12345" }`
- **Assertions**:
  - Response 201 Created.
  - **Capture**: `SupplierId` (UUID) for next steps.

#### Scenario: Define Material
- **Endpoint**: `POST /material/v1/materials`
- **Payload**: `{ "name": "Steel Rod", "supplierId": "{SupplierId}" }`
- **Assertions**:
  - Material linked to Supplier.

### 2.2. Purchase Orders (Known Issue)
#### Scenario: Create Purchase Order (Integration Test)
- **Goal**: Validate PO creation flow and verify ID compatibility.
- **Context**: **Potential Bug Identified**. SupplierService uses `Guid`, PO Service uses `Int`.
- **Steps**:
  1. Attempt `POST /purchase-order/v1.0/purchase-orders`.
  2. Payload: `{ "supplierID": "{SupplierId_from_2.1}", "items": [...] }`
- **Assertions (Expected Failure)**:
  - If the API rejects the Guid as an Int -> **Fail Test / Report Bug**.
  - If system handles mapping -> **Pass**.
- **Remediation**: This test serves as a regression check for the ID mismatch fix.
