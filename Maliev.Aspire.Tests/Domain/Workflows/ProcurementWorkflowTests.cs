using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Workflows;

/// <summary>
/// Integration tests for the procurement workflow spanning supplier, material, purchase order, invoice, and payment services.
/// </summary>
[Collection("AspireDomainTests")]
public class ProcurementWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests the full procurement workflow from supplier creation through material, purchase order, invoice, to payment.
    /// </summary>
    [Fact]
    public async Task FullProcurementWorkflow_SupplierToPayment()
    {
        var supplierClient = _fixture.CreateAuthenticatedClient("SupplierService");
        var materialClient = _fixture.CreateAuthenticatedClient("MaterialService");
        var poClient = _fixture.CreateAuthenticatedClient("PurchaseOrderService");
        var invoiceClient = _fixture.CreateAuthenticatedClient("InvoiceService");
        var paymentClient = _fixture.CreateAuthenticatedClient("PaymentService");

        var testId = Guid.NewGuid().ToString("N")[..8];

        var supplierResponse = await supplierClient.PostAsJsonAsync("/supplier/v1/suppliers", new
        {
            Name = $"SteelCo {testId}",
            Email = $"sales.{testId}@steelco.com",
            Country = "China",
            Status = "Active"
        });
        Assert.Equal(HttpStatusCode.Created, supplierResponse.StatusCode);
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<JsonElement>();
        var supplierId = supplier.GetProperty("id").GetGuid();
        _output.WriteLine($"[1] Supplier created: {supplierId}");

        var materialResponse = await materialClient.PostAsJsonAsync("/material/v1/materials", new
        {
            Name = $"PLA Filament {testId}",
            Sku = $"SKU-{testId}",
            Category = "Raw Materials",
            UnitPrice = 750.00m,
            Unit = "kg"
        });
        Assert.Equal(HttpStatusCode.Created, materialResponse.StatusCode);
        var material = await materialResponse.Content.ReadFromJsonAsync<JsonElement>();
        var materialId = material.GetProperty("id").GetGuid();
        _output.WriteLine($"[2] Material created: {materialId}");

        var poResponse = await poClient.PostAsJsonAsync("/purchase-order/v1/orders", new
        {
            SupplierId = supplierId,
            Date = DateTime.UtcNow,
            Items = new[]
            {
                new
                {
                    MaterialId = materialId,
                    Quantity = 50,
                    UnitPrice = 750.00m
                }
            }
        });
        var poContent = await poResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"[3] PO response: {poResponse.StatusCode} - {poContent}");
        Assert.Equal(HttpStatusCode.Created, poResponse.StatusCode);
        var po = await poResponse.Content.ReadFromJsonAsync<JsonElement>();
        _output.WriteLine($"[3] Purchase Order created");

        var invResponse = await invoiceClient.PostAsJsonAsync("/invoice/v1/invoices", new
        {
            CustomerId = Guid.NewGuid(),
            BillingIdentityType = 2,
            CustomerName = $"SteelCo {testId}",
            CustomerTaxId = "CN-9999",
            BillingAddress = "Shanghai, China",
            Currency = "THB",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Lines = new[]
            {
                new
                {
                    LineNumber = 1,
                    Description = "PLA Filament x50 kg",
                    Quantity = 50,
                    UnitPrice = 750.00m,
                    TaxCategory = "VAT",
                    TaxRate = 7.00m
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, invResponse.StatusCode);
        var invoice = await invResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = invoice.GetProperty("id").GetGuid();
        var totalAmount = invoice.GetProperty("totalAmount").GetDecimal();
        _output.WriteLine($"[4] Invoice created: {invoiceId} for {totalAmount} THB");

        var paymentIdempotencyKey = Guid.NewGuid().ToString();
        var payRequest = new HttpRequestMessage(HttpMethod.Post, "/payment/v1/payments")
        {
            Content = JsonContent.Create(new
            {
                Amount = totalAmount,
                Currency = "THB",
                CustomerId = supplierId.ToString(),
                Description = $"Payment for PO to supplier SteelCo"
            })
        };
        payRequest.Headers.Add("Idempotency-Key", paymentIdempotencyKey);

        var payResponse = await paymentClient.SendAsync(payRequest);
        Assert.Equal(HttpStatusCode.Created, payResponse.StatusCode);
        var payment = await payResponse.Content.ReadFromJsonAsync<JsonElement>();
        _output.WriteLine($"[5] Payment completed: {payment.GetProperty("transactionId").GetGuid()}");

        Assert.NotEqual(Guid.Empty, supplierId);
        Assert.NotEqual(Guid.Empty, materialId);
        Assert.NotEqual(Guid.Empty, invoiceId);
    }
}
