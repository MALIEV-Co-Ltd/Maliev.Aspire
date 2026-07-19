using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Integration;

/// <summary>
/// Integration tests verifying cross-service event chains via RabbitMQ messaging.
/// </summary>
[Collection("AspireDomainTests")]
public class EventChainTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Verifies that the order-payment workflow triggers a notification delivery via PaymentCompletedEvent.
    /// </summary>
    [Fact]
    public async Task OrderPaymentWorkflow_NotificationDelivered()
    {
        var orderClient = _fixture.CreateAuthenticatedClient("OrderService");
        var paymentClient = _fixture.CreateAuthenticatedClient("PaymentService");
        var notificationClient = _fixture.CreateAuthenticatedClient("NotificationService");

        var testId = Guid.NewGuid().ToString("N")[..8];

        var customer = await AspireTestData.CreateCustomerAsync(_fixture, "eventchain");
        var customerId = customer.GetProperty("id").GetGuid();
        _output.WriteLine($"[1] Customer: {customerId}");

        var orderResponse = await orderClient.PostAsJsonAsync("/order/v1/orders", new
        {
            CustomerId = customerId.ToString(),
            CustomerType = "Customer",
            ServiceCategoryId = 1,
            Requirements = $"Event chain test {testId}",
            OrderedQuantity = 1,
            CustomerPoNumber = $"PO-EVT-{testId}"
        });
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var order = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("orderId").GetString();
        _output.WriteLine($"[2] Order: {orderId}");

        var paymentIdempotencyKey = Guid.NewGuid().ToString();
        var paymentRequest = new HttpRequestMessage(HttpMethod.Post, "/payment/v1/payments")
        {
            Content = JsonContent.Create(new
            {
                Amount = 1500.00m,
                Currency = "THB",
                CustomerId = customerId.ToString(),
                OrderId = orderId,
                Description = $"Event chain payment {testId}",
                ReturnUrl = "https://example.com/payment/success",
                CancelUrl = "https://example.com/payment/cancel"
            })
        };
        paymentRequest.Headers.Add("Idempotency-Key", paymentIdempotencyKey);

        var payResponse = await paymentClient.SendAsync(paymentRequest);
        Assert.Equal(HttpStatusCode.Created, payResponse.StatusCode);
        var payment = await payResponse.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = payment.GetProperty("transactionId").GetGuid();
        _output.WriteLine($"[3] Payment: {transactionId}");

        var eventOrderId = Guid.NewGuid();
        var paymentTestResponse = await paymentClient.PostAsJsonAsync(
            "/payment/v1/test/publish-payment-completed", new
            {
                OrderId = eventOrderId,
                PaymentId = transactionId,
                Amount = 1500.00,
                Currency = "THB"
            });
        var publishContent = await paymentTestResponse.Content.ReadAsStringAsync();
        Assert.True(paymentTestResponse.IsSuccessStatusCode,
            $"Publish payment completed failed: {paymentTestResponse.StatusCode}: {publishContent}");
        using var publishDoc = JsonDocument.Parse(publishContent);
        var eventId = publishDoc.RootElement.GetProperty("eventId").GetGuid();
        _output.WriteLine("[4] PaymentCompletedEvent published");

        _ = await TestHelpers.WaitForAsync(
            async () =>
            {
                var r = await notificationClient.GetAsync(
                    $"/notification/v1/delivery-logs?eventId={eventId}&channelType=rabbitmq-event");
                return (Response: r, Content: await r.Content.ReadAsStringAsync());
            },
            until: result =>
            {
                if (!result.Response.IsSuccessStatusCode) return false;
                using var doc = JsonDocument.Parse(result.Content);
                var root = doc.RootElement;
                if (root.TryGetProperty("items", out var items))
                    return items.GetArrayLength() > 0;
                if (root.ValueKind == JsonValueKind.Array)
                    return root.GetArrayLength() > 0;
                return false;
            },
            timeout: TimeSpan.FromSeconds(30),
            interval: TimeSpan.FromSeconds(2),
            message: "NotificationService did not receive PaymentCompletedEvent within timeout");
        _output.WriteLine($"[5] Notification delivery log found");
    }

    /// <summary>
    /// Verifies that a customer created in CustomerService is resolvable when creating an order in OrderService.
    /// </summary>
    [Fact]
    public async Task CustomerCreated_CustomerResolvableInOrderService()
    {
        var orderClient = _fixture.CreateAuthenticatedClient("OrderService");

        var testId = Guid.NewGuid().ToString("N")[..8];

        var customer = await AspireTestData.CreateCustomerAsync(_fixture, "propagation");
        var customerId = customer.GetProperty("id").GetGuid();
        _output.WriteLine($"[1] Customer created: {customerId}");

        var orderResponse = await orderClient.PostAsJsonAsync("/order/v1/orders", new
        {
            CustomerId = customerId.ToString(),
            CustomerType = "Customer",
            ServiceCategoryId = 1,
            Requirements = $"Customer propagation test {testId}",
            OrderedQuantity = 1,
            CustomerPoNumber = $"PO-PROP-{testId}"
        });
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        _output.WriteLine($"[2] Order created for customer — customer resolved successfully");
    }

    /// <summary>
    /// Verifies that creating an employee triggers IAM principal provisioning via event propagation.
    /// </summary>
    [Fact]
    public async Task EmployeeCreated_IamPrincipalProvisioned()
    {
        var iamClient = _fixture.CreateAuthenticatedClient("IAMService");

        var testId = Guid.NewGuid().ToString("N")[..8];
        var testEmail = $"iamchain.{testId}@maliev.com";

        var employee = await AspireTestData.CreateEmployeeAsync(_fixture, "IAM", testEmail);
        var employeeId = employee.GetProperty("id").GetGuid();
        _output.WriteLine($"[1] Employee created: {employeeId}");

        _ = await TestHelpers.WaitForAsync(
            async () =>
            {
                var r = await iamClient.GetAsync($"/iam/v1/principals/by-email/{Uri.EscapeDataString(testEmail)}");
                return (Response: r, Content: await r.Content.ReadAsStringAsync());
            },
            until: result =>
            {
                if (!result.Response.IsSuccessStatusCode) return false;
                using var doc = JsonDocument.Parse(result.Content);
                var root = doc.RootElement;
                return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("principalId", out _);
            },
            timeout: TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromSeconds(2),
            message: $"IAM principal for {testEmail} not found within timeout");
        _output.WriteLine($"[2] IAM principal provisioned for employee");
    }

    /// <summary>
    /// Verifies that finalizing an invoice triggers PDF generation via InvoiceGeneratedEvent.
    /// </summary>
    [Fact]
    public async Task InvoiceFinalized_PdfGenerationTriggered()
    {
        var invoiceClient = _fixture.CreateAuthenticatedClient("InvoiceService");
        var pdfClient = _fixture.CreateAuthenticatedClient("PdfService");

        var customer = await AspireTestData.CreateCorporateCustomerAsync(_fixture, "pdfchain");
        var customerId = customer.GetProperty("id").GetGuid();
        var customerName = customer.GetProperty("name").GetString();

        var invResponse = await invoiceClient.PostAsJsonAsync("/invoice/v1/invoices", new
        {
            CustomerId = customerId,
            BillingIdentityType = 1,
            CustomerName = customerName,
            CustomerTaxId = "3333333333333",
            BillingAddress = "456 PDF Test Ave, Bangkok",
            Currency = "THB",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            Lines = new[]
            {
                new
                {
                    LineNumber = 1,
                    Description = "SLA Resin Print",
                    Quantity = 2,
                    UnitPrice = 2500.00m,
                    TaxCategory = "VAT",
                    TaxRate = 7.00m
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, invResponse.StatusCode);
        var invoice = await invResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = invoice.GetProperty("id").GetGuid();
        _output.WriteLine($"[1] Invoice created: {invoiceId}");

        var finalizeIdempotencyKey = Guid.NewGuid().ToString();
        var finalizeRequest = new HttpRequestMessage(HttpMethod.Post,
            $"/invoice/v1/invoices/{invoiceId}/finalize")
        {
            Content = JsonContent.Create(new { FinalizedBy = "aspire-system-test" })
        };
        finalizeRequest.Headers.Add("Idempotency-Key", finalizeIdempotencyKey);

        var finalizeResponse = await invoiceClient.SendAsync(finalizeRequest);
        var finalizeContent = await finalizeResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"[2] Finalize response: {finalizeResponse.StatusCode} - {finalizeContent}");
        Assert.True(finalizeResponse.IsSuccessStatusCode,
            $"Finalize failed: {finalizeResponse.StatusCode}: {finalizeContent}");

        _ = await TestHelpers.WaitForAsync(
            async () =>
            {
                var r = await pdfClient.GetAsync($"/pdf/v1/generations/latest?documentType=Invoice&referenceId={invoiceId}");
                return (Response: r, Content: await r.Content.ReadAsStringAsync());
            },
            until: result =>
            {
                if (!result.Response.IsSuccessStatusCode) return false;
                using var doc = JsonDocument.Parse(result.Content);
                var root = doc.RootElement;
                return root.TryGetProperty("storageUrl", out var storageUrl)
                    && !string.IsNullOrWhiteSpace(storageUrl.GetString());
            },
            timeout: TimeSpan.FromSeconds(30),
            interval: TimeSpan.FromSeconds(3),
            message: $"PDF generation for invoice {invoiceId} not found within timeout");
        _output.WriteLine($"[3] PDF generated for invoice");
    }
}
