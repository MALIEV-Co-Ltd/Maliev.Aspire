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
        var customer = await AspireTestData.CreateCorporateCustomerAsync(_fixture, "procurement");
        var customerId = customer.GetProperty("id").GetGuid();
        var order = await AspireTestData.CreateOrderAsync(_fixture, customerId, $"Procurement workflow test {testId}");
        var orderId = order.GetProperty("orderId").GetString()
            ?? throw new InvalidOperationException("OrderService did not return orderId.");

        var supplierResponse = await supplierClient.PostAsJsonAsync("/supplier/v1/suppliers", new
        {
            CompanyName = $"SteelCo {testId}",
            TaxId = $"CN-{testId}",
            Address = "88 Supply Road",
            City = "Shanghai",
            Country = "China",
            PostalCode = "200000",
            Capabilities = new[] { "Filament supply" },
            PrimaryContact = new
            {
                Name = "Procurement Contact",
                Email = $"sales.{testId}@steelco.com",
                Role = "Sales",
                Phone = "+862112345678"
            }
        });
        Assert.Equal(HttpStatusCode.Created, supplierResponse.StatusCode);
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<JsonElement>();
        var supplierId = supplier.GetProperty("id").GetGuid();
        _output.WriteLine($"[1] Supplier created: {supplierId}");

        var materialResponse = await materialClient.PostAsJsonAsync("/material/v1/materials", new
        {
            Name = $"PLA Filament {testId}",
            Code = $"PLA-{testId}",
            PricePerUnit = 750.00m,
            StockLevel = 50
        });
        Assert.Equal(HttpStatusCode.Created, materialResponse.StatusCode);
        var material = await materialResponse.Content.ReadFromJsonAsync<JsonElement>();
        var materialId = material.GetProperty("id").GetGuid();
        _output.WriteLine($"[2] Material created: {materialId}");

        var poResponse = await poClient.PostAsJsonAsync("/purchase-order/v1/purchase-orders", new
        {
            OrderType = 0,
            SupplierID = 1,
            SupplierServiceId = supplierId,
            OrderID = 1,
            SourceOrderId = orderId,
            CurrencyID = 1,
            CurrencyCode = "THB",
            WHTRate = 0m,
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(14),
            ShippingAddress = new
            {
                AddressType = 0,
                CompanyName = "MALIEV",
                ContactName = "Procurement",
                AddressLine1 = "123 Integration Test Road",
                City = "Bangkok",
                PostalCode = "10110",
                Country = "Thailand"
            },
            Items = new[]
            {
                new
                {
                    SourceOrderItemId = "primary",
                    Quantity = 1m
                }
            }
        });
        var poContent = await poResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"[3] PO response: {poResponse.StatusCode} - {poContent}");
        Assert.Equal(HttpStatusCode.Created, poResponse.StatusCode);
        var po = await poResponse.Content.ReadFromJsonAsync<JsonElement>();
        var poOrderNumber = po.GetProperty("orderNumber").GetString()
            ?? throw new InvalidOperationException("PurchaseOrderService did not return orderNumber.");
        _output.WriteLine($"[3] Purchase Order created");

        var invResponse = await invoiceClient.PostAsJsonAsync("/invoice/v1/invoices", new
        {
            customerId,
            billingIdentityType = 1,
            customerName = $"Procurement customer {testId}",
            customerTaxId = "0999999999999",
            billingAddress = "123 Integration Test Road, Bangkok",
            currency = "THB",
            issueDate = DateTime.UtcNow,
            dueDate = DateTime.UtcNow.AddDays(30),
            lines = new[]
            {
                new
                {
                    lineNumber = 1,
                    description = "PLA Filament x50 kg",
                    quantity = 50m,
                    unitPrice = 750.00m,
                    taxCategory = "VAT",
                    taxRate = 7.00m
                }
            }
        });
        var invContent = await invResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"[4] Invoice response: {invResponse.StatusCode} - {invContent}");
        Assert.True(invResponse.StatusCode == HttpStatusCode.Created, $"Expected Created but got {invResponse.StatusCode}: {invContent}");
        var invoice = await invResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = invoice.GetProperty("id").GetGuid();
        var totalAmount = invoice.GetProperty("grandTotal").GetDecimal();
        _output.WriteLine($"[4] Invoice created: {invoiceId} for {totalAmount} THB");

        var paymentIdempotencyKey = Guid.NewGuid().ToString();
        var payRequest = new HttpRequestMessage(HttpMethod.Post, "/payment/v1/payments")
        {
            Content = JsonContent.Create(new
            {
                Amount = totalAmount,
                Currency = "THB",
                CustomerId = supplierId.ToString(),
                OrderId = poOrderNumber,
                Description = $"Payment for PO to supplier SteelCo",
                ReturnUrl = "https://example.com/payment/success",
                CancelUrl = "https://example.com/payment/cancel"
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
