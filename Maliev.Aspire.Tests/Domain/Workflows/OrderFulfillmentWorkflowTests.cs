using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Workflows;

/// <summary>
/// Integration tests for the full order fulfillment workflow spanning customer, order, invoice, payment, receipt, and delivery services.
/// </summary>
[Collection("AspireDomainTests")]
public class OrderFulfillmentWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests the complete order fulfillment workflow from customer creation through order, invoice, payment, receipt, and delivery.
    /// </summary>
    [Fact]
    public async Task FullOrderFulfillment_CustomerToOrderToPaymentToDelivery()
    {
        var orderClient = _fixture.CreateAuthenticatedClient("OrderService");
        var invoiceClient = _fixture.CreateAuthenticatedClient("InvoiceService");
        var paymentClient = _fixture.CreateAuthenticatedClient("PaymentService");
        var deliveryClient = _fixture.CreateAuthenticatedClient("DeliveryService");
        var receiptClient = _fixture.CreateAuthenticatedClient("ReceiptService");

        var testId = Guid.NewGuid().ToString("N")[..8];

        var customer = await AspireTestData.CreateCorporateCustomerAsync(_fixture, "orderflow");
        var customerId = customer.GetProperty("id").GetGuid();
        var customerName = customer.GetProperty("name").GetString();
        _output.WriteLine($"[1] Customer created: {customerName} ({customerId})");

        var orderResponse = await orderClient.PostAsJsonAsync("/order/v1/orders", new
        {
            CustomerId = customerId.ToString(),
            CustomerType = "Customer",
            ServiceCategoryId = 1,
            Requirements = $"Order fulfillment test {testId}",
            OrderedQuantity = 5,
            CustomerPoNumber = $"PO-{testId}"
        });
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var order = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("orderId").GetString();
        _output.WriteLine($"[2] Order created: {orderId}");

        var invResponse = await invoiceClient.PostAsJsonAsync("/invoice/v1/invoices", new
        {
            CustomerId = customerId,
            BillingIdentityType = 1,
            CustomerName = customerName,
            CustomerTaxId = "1111111111111",
            BillingAddress = "123 Test Road, Bangkok 10110",
            Currency = "THB",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(7),
            Lines = new[]
            {
                new
                {
                    LineNumber = 1,
                    Description = "FDM 3D Printing Service",
                    Quantity = 5,
                    UnitPrice = 3000.00m,
                    TaxCategory = "VAT",
                    TaxRate = 7.00m
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, invResponse.StatusCode);
        var invoice = await invResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = invoice.GetProperty("id").GetGuid();
        invoice = await AspireTestData.FinalizeInvoiceAsync(_fixture, invoiceId);
        var totalAmount = invoice.GetProperty("grandTotal").GetDecimal();
        _output.WriteLine($"[3] Invoice created: {invoiceId} for {totalAmount} THB");

        var paymentIdempotencyKey = Guid.NewGuid().ToString();
        var paymentRequest = new HttpRequestMessage(HttpMethod.Post, "/payment/v1/payments")
        {
            Content = JsonContent.Create(new
            {
                Amount = totalAmount,
                Currency = "THB",
                CustomerId = customerId.ToString(),
                OrderId = orderId,
                Description = $"Payment for order {orderId}",
                ReturnUrl = "https://example.com/payment/success",
                CancelUrl = "https://example.com/payment/cancel"
            })
        };
        paymentRequest.Headers.Add("Idempotency-Key", paymentIdempotencyKey);

        var payResponse = await paymentClient.SendAsync(paymentRequest);
        Assert.Equal(HttpStatusCode.Created, payResponse.StatusCode);
        var payment = await payResponse.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = payment.GetProperty("transactionId").GetGuid();
        _output.WriteLine($"[4] Payment recorded: {transactionId}");

        var receiptResponse = await receiptClient.PostAsJsonAsync("/receipt/v1/receipts", new
        {
            InvoiceId = invoiceId,
            Amount = totalAmount,
            PaymentMethod = "BankTransfer"
        });
        Assert.Equal(HttpStatusCode.Created, receiptResponse.StatusCode);
        var receipt = await receiptResponse.Content.ReadFromJsonAsync<JsonElement>();
        var receiptId = receipt.GetProperty("id").GetGuid();
        _output.WriteLine($"[5] Receipt created: {receiptId}");

        var deliveryResponse = await deliveryClient.PostAsJsonAsync("/delivery/v1/delivery-notes", new
        {
            OrderId = orderId,
            CustomerId = customerId,
            DeliveryDate = DateTime.UtcNow.AddDays(2),
            Items = new[]
            {
                new
                {
                    ProductCode = "FDM-PART",
                    ProductName = "FDM 3D Printed Part",
                    QuantityOrdered = 5m,
                    QuantityManufactured = 5m,
                    QuantityDelivered = 5m,
                    UnitOfMeasure = "pcs"
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, deliveryResponse.StatusCode);
        var deliveryNote = await deliveryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var dnId = deliveryNote.GetProperty("deliveryNoteId").GetString();
        _output.WriteLine($"[6] Delivery note created: {dnId}");

        var transitResponse = await deliveryClient.PatchAsJsonAsync(
            $"/delivery/v1/delivery-notes/{dnId}/status", new { NewStatus = "InTransit" });
        Assert.Equal(HttpStatusCode.OK, transitResponse.StatusCode);

        var deliveredResponse = await deliveryClient.PatchAsJsonAsync(
            $"/delivery/v1/delivery-notes/{dnId}/status",
            new { NewStatus = "Delivered", ActualDeliveryTime = DateTime.UtcNow, ReceivedByName = "Test Receiver" });
        Assert.Equal(HttpStatusCode.OK, deliveredResponse.StatusCode);
        _output.WriteLine($"[7] Delivery completed: {dnId}");

        var finalOrderResponse = await orderClient.GetAsync($"/order/v1/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, finalOrderResponse.StatusCode);
        _output.WriteLine("[8] Order verified successfully");

        Assert.NotEqual(Guid.Empty, customerId);
        Assert.False(string.IsNullOrEmpty(orderId));
        Assert.NotEqual(Guid.Empty, invoiceId);
        Assert.NotEqual(Guid.Empty, transactionId);
        Assert.NotEqual(Guid.Empty, receiptId);
        Assert.False(string.IsNullOrEmpty(dnId));
    }
}
