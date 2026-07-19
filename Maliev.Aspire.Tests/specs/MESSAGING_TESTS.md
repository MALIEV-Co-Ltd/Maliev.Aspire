# Messaging & Event Chain Test Specifications

> Defines test scenarios for verifying RabbitMQ event flows across services via MassTransit.

## Existing Coverage

| Test | Services | File |
|------|----------|------|
| Payment → Notification delivery log | PaymentService, NotificationService | `MessagingTests.cs` |

## Planned Event Chain Tests

### 1. Order Payment Notification (End-to-End)

**Flow**: Create Order → Complete Payment → Verify Notification Logged

| Step | Service | Action | Assertion |
|------|---------|--------|-----------|
| 1 | CustomerService | Create test customer | 201 Created |
| 2 | OrderService | Create order for customer | 201 Created, returns orderId |
| 3 | PaymentService | Complete payment for order | 200 OK, publishes `PaymentCompletedEvent` |
| 4 | NotificationService | Poll delivery logs | Log entry with `rabbitmq-event` channel, `received` status |
| 5 | OrderService | Get order by ID | Order status updated (payment reflected) |

**Polling**: Use `TestHelpers.WaitForAsync` with 30s timeout for steps 4 and 5.

---

### 2. Invoice Finalized → PDF Generation

**Flow**: Create Invoice → Finalize → Verify PDF Generation Event Consumed

| Step | Service | Action | Assertion |
|------|---------|--------|-----------|
| 1 | InvoiceService | Create draft invoice | 201 Created |
| 2 | InvoiceService | Finalize invoice | 200 OK, publishes `InvoiceFinalizedEvent` |
| 3 | PdfService | Poll for generated PDF | PDF record created with invoice reference |

---

### 3. Customer Created → Propagation

**Flow**: Create Customer → Verify Customer Data Available in Dependent Services

| Step | Service | Action | Assertion |
|------|---------|--------|-----------|
| 1 | CustomerService | Create customer with company | 201 Created, publishes `CustomerCreatedEvent` |
| 2 | OrderService | Attempt to create order for new customer | Order service can resolve customer |

---

### 4. Employee Onboarding Lifecycle

**Flow**: Create Employee → IAM Provisioning → Leave Allocation → Career Record

| Step | Service | Action | Assertion |
|------|---------|--------|-----------|
| 1 | EmployeeService | Create employee | 201 Created, publishes `EmployeeCreatedEvent` |
| 2 | IAMService | Poll for principal record | Principal created for employee |
| 3 | LeaveService | Poll for leave balance | Default leave allocation created |
| 4 | CareerService | Poll for career record | Career record initialized |

---

### 5. File Upload → Preview Images → Order/Project Update

**Flow**: Upload File → Geometry Processing → Preview Images Generated → Consumer Update

| Step | Service | Action | Assertion |
|------|---------|--------|-----------|
| 1 | UploadService | Upload test file | 201 Created, publishes file event |
| 2 | GeometryService | Process file (generates preview) | Publishes `PreviewImagesGeneratedEvent` |
| 3 | OrderService/ProjectService | Poll for preview image link | Preview image reference stored |

**Note**: This chain involves the Python GeometryService and may require special test setup.

---

## Implementation Notes

- All event chain tests belong in `Integration/EventChainTests.cs`
- Use `[Collection("AspireDomainTests")]` to share the AppHost
- Use `[Trait("Tier", "SystemIntegration")]` on all tests
- Use `TestHelpers.WaitForAsync` for all async polling (never `Task.Delay`)
- Each test should create unique data (use `Guid.NewGuid()`) to avoid conflicts with other tests
