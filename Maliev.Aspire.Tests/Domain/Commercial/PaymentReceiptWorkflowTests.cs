using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Commercial;

/// <summary>
/// Integration tests for payment and receipt workflow.
/// </summary>
[Collection("AspireDomainTests")]
public class PaymentReceiptWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests the full payment workflow from invoice creation through payment to receipt generation.
    /// </summary>
    [Fact]
    public async Task FullPaymentWorkflow_InvoiceToPaymentToReceipt()
    {
        var customerClient = _fixture.CreateAuthenticatedClient("CustomerService");
        var invoiceClient = _fixture.CreateAuthenticatedClient("InvoiceService");
        var paymentClient = _fixture.CreateAuthenticatedClient("PaymentService");
        var receiptClient = _fixture.CreateAuthenticatedClient("ReceiptService");

        // 1. Create Customer
        var createCustomerRequest = new
        {
            FirstName = "Payment",
            LastName = "Workflow",
            Email = $"pay.test.{Guid.NewGuid():N}@example.com",
            Type = "Corporate",
            TaxId = "9999999999999"
        };
        var custResponse = await customerClient.PostAsJsonAsync("/customer/v1/customers", createCustomerRequest);
        Assert.Equal(HttpStatusCode.Created, custResponse.StatusCode);
        var customer = await custResponse.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = customer.GetProperty("id").GetGuid();
        var customerName = customer.GetProperty("name").GetString();

        // 2. Create Invoice
        var createInvoiceRequest = new
        {
            CustomerId = customerId,
            BillingIdentityType = 1, // Corporate
            CustomerName = customerName,
            CustomerTaxId = "9999999999999",
            BillingAddress = "Payment Test Street",
            Currency = "THB",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(7),
            Lines = new[]
            {
                new
                {
                    LineNumber = 1,
                    Description = "Service Item",
                    Quantity = 1,
                    UnitPrice = 5000.00m,
                    TaxCategory = "VAT",
                    TaxRate = 7.00m
                }
            }
        };
        var invResponse = await invoiceClient.PostAsJsonAsync("/invoice/v1/invoices", createInvoiceRequest);
        Assert.Equal(HttpStatusCode.Created, invResponse.StatusCode);
        var invoice = await invResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = invoice.GetProperty("id").GetGuid();
        var totalAmount = invoice.GetProperty("totalAmount").GetDecimal();
        _output.WriteLine($"Invoice created: {invoiceId} for {totalAmount} THB");

        // 3. Create Payment
        var paymentIdempotencyKey = Guid.NewGuid().ToString();
        var paymentRequest = new
        {
            Amount = totalAmount,
            Currency = "THB",
            CustomerId = customerId.ToString(),
            OrderId = Guid.NewGuid().ToString(), // Dummy order link
            Description = $"Payment for invoice {invoiceId}"
        };

        var payReqMsg = new HttpRequestMessage(HttpMethod.Post, "/payment/v1/payments")
        {
            Content = JsonContent.Create(paymentRequest)
        };
        payReqMsg.Headers.Add("Idempotency-Key", paymentIdempotencyKey);

        var payResponse = await paymentClient.SendAsync(payReqMsg);
        Assert.Equal(HttpStatusCode.Created, payResponse.StatusCode);
        var payment = await payResponse.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = payment.GetProperty("transactionId").GetGuid();
        _output.WriteLine($"Payment processed: {transactionId}");

        // 4. Create Receipt
        var createReceiptRequest = new
        {
            InvoiceId = invoiceId,
            PaymentTransactionId = transactionId,
            PaymentMethod = "BankTransfer",
            AmountPaid = totalAmount,
            Currency = "THB",
            Notes = "Receipt from integration test"
        };
        var receiptResponse = await receiptClient.PostAsJsonAsync("/receipt/v1/receipts", createReceiptRequest);
        Assert.Equal(HttpStatusCode.Created, receiptResponse.StatusCode);
        var receipt = await receiptResponse.Content.ReadFromJsonAsync<JsonElement>();
        var receiptId = receipt.GetProperty("id").GetGuid();
        _output.WriteLine($"Receipt created: {receiptId}");

        Assert.Equal(totalAmount, receipt.GetProperty("totalAmount").GetDecimal());
    }
}
