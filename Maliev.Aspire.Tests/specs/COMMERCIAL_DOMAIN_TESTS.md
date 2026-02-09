# Commercial Domain Test Specification

## 1. Scope
Validates the "Order-to-Cash" lifecycle involving:
- **Customer Service**: CRM.
- **Order Service**: Sales Orders.
- **Invoice Service**: Billing.
- **Payment Service**: Collections.

## 2. Test Scenarios

### 2.1. Customer Management
#### Scenario: Create Retail Customer
- **Endpoint**: `POST /customer/v1/customers`
- **Payload**: `{ "firstName": "Alice", "email": "alice@client.com", "segment": "Retail" }`
- **Assertions**:
  - Customer Created (201).
  - Welcome Email Notification triggered (check Notification Service).

### 2.2. Order Fulfillment (E2E)
#### Scenario: Standard Order Process
- **Goal**: Verify order flow from creation to payment.
- **Steps**:
  1. **Create Order**: `POST /order/v1/orders`
     - Link to `CustomerId` from 2.1.
     - Add items (Service: 3D Printing).
     - Assert: Status `Draft` / `Pending`.
  2. **Invoice Generation**:
     - Trigger: Order Status Change or Manual Action.
     - Check `InvoiceService` for new Invoice linked to `OrderId`.
     - Assert: Invoice Amount matches Order Total.
  3. **Payment**:
     - `POST /payment/v1/payments`
     - Payload: `{ "orderId": "...", "amount": 100.00, "currency": "USD" }`
     - Assert: Payment Success.
  4. **Final State**:
     - Order Status -> `Paid` / `Processing`.
     - Invoice Status -> `Paid`.

#### Scenario: Invalid Customer Reference
- **Action**: Create Order with non-existent `CustomerId`.
- **Assertions**:
  - Response 400 Bad Request or 404 Not Found.
  - Error indicates "Customer does not exist".
