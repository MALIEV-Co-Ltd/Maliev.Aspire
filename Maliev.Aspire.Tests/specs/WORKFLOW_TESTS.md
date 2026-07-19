# Cross-Service Workflow Test Specifications

> Defines test scenarios for verifying multi-service business workflows at the Aspire system integration level.

## Purpose

Workflow tests verify that business processes spanning multiple services work correctly end-to-end. Unlike event chain tests (which focus on message propagation), workflow tests verify the **business outcome** of multi-step processes through HTTP API calls.

---

## Planned Workflow Tests

### 1. Order Fulfillment Workflow

**Location**: `Domain/Workflows/OrderFulfillmentWorkflowTests.cs`

**Flow**: Customer → Order → Payment → Delivery

| Step | Service | Endpoint | Method | Assertion |
|------|---------|----------|--------|-----------|
| 1 | CountryService | `/country/v1/countries` | POST | Seed test country (if not exists) |
| 2 | CustomerService | `/customer/v1/customers` | POST | Create customer with company, 201 |
| 3 | OrderService | `/order/v1/orders` | POST | Create order for customer, 201 |
| 4 | PaymentService | `/payment/v1/payments` | POST | Record payment for order, 201 |
| 5 | DeliveryService | `/delivery/v1/deliveries` | POST | Create delivery for order, 201 |
| 6 | DeliveryService | `/delivery/v1/deliveries/{id}/status` | PATCH | Update to InTransit, then Delivered |
| 7 | OrderService | `/order/v1/orders/{id}` | GET | Verify order reflects payment status |

---

### 2. Procurement Workflow

**Location**: `Domain/Workflows/ProcurementWorkflowTests.cs`

**Flow**: Supplier → Material → PurchaseOrder → Invoice → Payment

| Step | Service | Endpoint | Method | Assertion |
|------|---------|----------|--------|-----------|
| 1 | SupplierService | `/supplier/v1/suppliers` | POST | Create supplier, 201 |
| 2 | MaterialService | `/material/v1/materials` | POST | Create material, 201 |
| 3 | PurchaseOrderService | `/purchaseorder/v1/purchase-orders` | POST | Create PO for supplier/material, 201 |
| 4 | InvoiceService | `/invoice/v1/invoices` | POST | Create invoice from PO, 201 |
| 5 | PaymentService | `/payment/v1/payments` | POST | Record payment for invoice, 201 |

---

### 3. Employee Lifecycle Workflow

**Location**: `Domain/Workflows/EmployeeLifecycleWorkflowTests.cs`

**Flow**: Employee → IAM → Leave → Career → Compensation

| Step | Service | Endpoint | Method | Assertion |
|------|---------|----------|--------|-----------|
| 1 | EmployeeService | `/employee/v1/employees` | POST | Create employee, 201 |
| 2 | AuthService | `/auth/v1/exchange/google` | POST | Simulate first login |
| 3 | IAMService | `/iam/v1/principals` | GET | Verify principal created |
| 4 | LeaveService | `/leave/v1/leave-balances` | GET | Verify default leave balance |
| 5 | CareerService | `/career/v1/careers` | GET | Verify career record |
| 6 | CompensationService | `/compensation/v1/compensations` | POST | Create compensation record, 201 |
| 7 | PerformanceService | `/performance/v1/reviews` | POST | Create performance review, 201 |

---

### 4. Quotation to Invoice Workflow (Extended)

**Location**: Extends existing `QuotationToInvoiceWorkflowTests.cs`

**Flow**: Customer → Quotation → Order → Invoice → Payment → Receipt

| Step | Service | Endpoint | Method | Assertion |
|------|---------|----------|--------|-----------|
| 1 | CustomerService | Create customer | POST | 201 |
| 2 | QuotationService | Create quotation | POST | 201 |
| 3 | QuotationService | Approve quotation | PATCH | 200 |
| 4 | OrderService | Create order from quotation | POST | 201 |
| 5 | InvoiceService | Generate invoice from order | POST | 201 |
| 6 | PaymentService | Record payment | POST | 201 |
| 7 | ReceiptService | Generate receipt | POST | 201 |

---

## Error Scenario Tests

**Location**: `Integration/ErrorScenarioTests.cs`

| Test | Scenario | Assertion |
|------|----------|-----------|
| `UnauthorizedAccess_Returns401` | Request without Bearer token to protected endpoint | 401 Unauthorized |
| `InvalidPermission_Returns403` | Request with JWT lacking required permission | 403 Forbidden |
| `NonExistentResource_Returns404` | GET for `Guid.NewGuid()` entity | 404 Not Found |
| `MalformedRequest_Returns400` | POST with invalid/missing required fields | 400 Bad Request |

---

## Implementation Notes

- All workflow tests belong in `Domain/Workflows/` directory
- Use `[Collection("AspireDomainTests")]` to share the AppHost
- Use `[Trait("Tier", "SystemIntegration")]` on all tests
- Each workflow test should be self-contained: seed all required data within the test
- Use unique identifiers (`Guid.NewGuid()`) for all test data
- Use `TestHelpers.WaitForAsync` for any eventual consistency checks
- Document the expected API response structure inline as DTOs within the test file (not in shared projects)
