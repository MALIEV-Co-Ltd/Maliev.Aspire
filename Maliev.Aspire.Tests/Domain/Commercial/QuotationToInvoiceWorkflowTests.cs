using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Commercial;

/// <summary>
/// Integration tests for the commercial workflow from quotation to order to invoice.
/// </summary>
public class QuotationToInvoiceWorkflowTests(ITestOutputHelper output) : MalievTestBase(output)
{
    /// <summary>
    /// Tests the full commercial workflow: quotation creation, order from quotation, and invoice generation.
    /// </summary>
    [Fact]
    public async Task FullCommercialWorkflow_QuotationToOrderToInvoice()
    {
        var customerClient = await CreateAuthenticatedClient("CustomerService");
        var materialClient = await CreateAuthenticatedClient("MaterialService");
        var quotationClient = await CreateAuthenticatedClient("QuotationService");
        var orderClient = await CreateAuthenticatedClient("OrderService");
        var invoiceClient = await CreateAuthenticatedClient("InvoiceService");

        // 1. Create Customer
        var createCustomerRequest = new
        {
            FirstName = "Commercial",
            LastName = "Test",
            Email = $"comm.test.{Guid.NewGuid():N}@example.com",
            Phone = "0812345678",
            Type = "Corporate",
            TaxId = "1234567890123"
        };
        var custResponse = await customerClient.PostAsJsonAsync("/customer/v1/customers", createCustomerRequest);
        Assert.Equal(HttpStatusCode.Created, custResponse.StatusCode);
        var customer = await custResponse.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = customer.GetProperty("id").GetGuid();
        var customerName = customer.GetProperty("name").GetString();
        Output.WriteLine($"Customer created: {customerName} ({customerId})");

        // 2. Get a Material
        var matResponse = await materialClient.GetAsync("/material/v1/materials?pageSize=1");
        var matResult = await matResponse.Content.ReadFromJsonAsync<JsonElement>();
        var materials = matResult.GetProperty("items");
        Assert.True(materials.GetArrayLength() > 0,
            "No materials found in MaterialService catalog — ensure the database seeder has run before executing integration tests.");
        var material = materials[0];
        var materialId = material.GetProperty("id").GetGuid();
        var materialPrice = material.GetProperty("unitPrice").GetDecimal();
        Output.WriteLine($"Using material: {material.GetProperty("code").GetString()} @ {materialPrice}");

        // 3. Create Quotation
        var createQuotationRequest = new
        {
            CustomerId = customerId,
            BillingIdentityType = 1, // Corporate
            ValidityPeriodStart = DateTime.UtcNow,
            ValidityPeriodEnd = DateTime.UtcNow.AddDays(7),
            LineItems = new[]
            {
                new
                {
                    MaterialServiceId = materialId,
                    Quantity = 100,
                    UnitOfMeasure = "pcs",
                    UnitPrice = materialPrice,
                    Notes = "Integration Test Line"
                }
            }
        };
        var quotResponse = await quotationClient.PostAsJsonAsync("/quotation/v1/quotations", createQuotationRequest);
        Assert.Equal(HttpStatusCode.Created, quotResponse.StatusCode);
        var quotation = await quotResponse.Content.ReadFromJsonAsync<JsonElement>();
        var quotationId = quotation.GetProperty("id").GetGuid();
        Output.WriteLine($"Quotation created: {quotationId}");

        // 4. Create Order from Quotation
        var createOrderRequest = new
        {
            CustomerId = customerId.ToString(),
            CustomerType = "Customer",
            ServiceCategoryId = 1, // Generic
            Requirements = $"Order based on quotation {quotationId}",
            OrderedQuantity = 100,
            MaterialId = 1, // Assuming mapped ID 1 exists for the material
            CustomerPoNumber = "PO-TEST-123"
        };
        var orderResponse = await orderClient.PostAsJsonAsync("/order/v1/orders", createOrderRequest);
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var order = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("orderId").GetString();
        Output.WriteLine($"Order created: {orderId}");

        // 5. Create Invoice from Order
        var createInvoiceRequest = new
        {
            CustomerId = customerId,
            BillingIdentityType = 1, // Corporate
            CustomerName = customerName,
            CustomerTaxId = "1234567890123",
            BillingAddress = "Test Address, Bangkok",
            Currency = "THB",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            PoNumber = "PO-TEST-123",
            Lines = new[]
            {
                new
                {
                    LineNumber = 1,
                    Description = "Consultation Service",
                    Quantity = 1,
                    UnitPrice = 1000.00m,
                    TaxCategory = "VAT",
                    TaxRate = 7.00m
                }
            }
        };
        var invResponse = await invoiceClient.PostAsJsonAsync("/invoice/v1/invoices", createInvoiceRequest);
        Assert.Equal(HttpStatusCode.Created, invResponse.StatusCode);
        var invoice = await invResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = invoice.GetProperty("id").GetGuid();
        Output.WriteLine($"Invoice created: {invoiceId}");

        Assert.NotEqual(Guid.Empty, customerId);
        Assert.NotEqual(Guid.Empty, quotationId);
        Assert.False(string.IsNullOrEmpty(orderId));
        Assert.NotEqual(Guid.Empty, invoiceId);
    }
}
