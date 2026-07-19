using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Commercial;

/// <summary>
/// Integration tests for the commercial workflow from quotation to order to invoice.
/// </summary>
[Collection("AspireDomainTests")]
public class QuotationToInvoiceWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests the full commercial workflow: quotation creation, order from quotation, and invoice generation.
    /// </summary>
    [Fact]
    public async Task FullCommercialWorkflow_QuotationToOrderToInvoice()
    {
        var materialClient = _fixture.CreateAuthenticatedClient("MaterialService");
        var quotationClient = _fixture.CreateAuthenticatedClient("QuotationService");
        var orderClient = _fixture.CreateAuthenticatedClient("OrderService");
        var invoiceClient = _fixture.CreateAuthenticatedClient("InvoiceService");

        // 1. Create Customer
        var customer = await AspireTestData.CreateCorporateCustomerAsync(_fixture, "commercial");
        var customerId = customer.GetProperty("id").GetGuid();
        var customerName = GetOptionalString(customer, "name") ?? "Commercial Test";
        _output.WriteLine($"Customer created: {customerName} ({customerId})");

        // 2. Get a Material
        var matResponse = await materialClient.GetAsync("/material/v1/materials?pageSize=1");
        var matResult = await matResponse.Content.ReadFromJsonAsync<JsonElement>();
        var materials = GetPagedItems(matResult);
        Assert.True(materials.GetArrayLength() > 0,
            "No materials found in MaterialService catalog — ensure the database seeder has run before executing integration tests.");
        var material = materials[0];
        var materialId = material.GetProperty("id").GetGuid();
        var materialPrice = GetRequiredDecimal(material, "pricePerUnit", "unitPrice");
        var materialCode = GetOptionalString(material, "code") ?? GetOptionalString(material, "name") ?? materialId.ToString();
        _output.WriteLine($"Using material: {materialCode} @ {materialPrice}");

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
        _output.WriteLine($"Quotation created: {quotationId}");

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
        _output.WriteLine($"Order created: {orderId}");

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
        var invoiceContent = await invResponse.Content.ReadAsStringAsync();
        Assert.True(
            invResponse.StatusCode == HttpStatusCode.Created,
            $"Expected Created but got {invResponse.StatusCode}: {invoiceContent}");
        using var invoiceDocument = JsonDocument.Parse(invoiceContent);
        var invoice = invoiceDocument.RootElement.Clone();
        var invoiceId = invoice.GetProperty("id").GetGuid();
        _output.WriteLine($"Invoice created: {invoiceId}");

        Assert.NotEqual(Guid.Empty, customerId);
        Assert.NotEqual(Guid.Empty, quotationId);
        Assert.False(string.IsNullOrEmpty(orderId));
        Assert.NotEqual(Guid.Empty, invoiceId);
    }

    private static JsonElement GetPagedItems(JsonElement result)
    {
        if (result.ValueKind == JsonValueKind.Array)
        {
            return result;
        }

        if (result.TryGetProperty("items", out var items))
        {
            return items;
        }

        if (result.TryGetProperty("data", out var data))
        {
            return data;
        }

        throw new InvalidOperationException("MaterialService response did not contain an items or data collection.");
    }

    private static decimal GetRequiredDecimal(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var value))
            {
                return value.GetDecimal();
            }
        }

        throw new InvalidOperationException(
            $"MaterialService response did not contain any of these decimal properties: {string.Join(", ", propertyNames)}.");
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
    }
}
