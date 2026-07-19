using System.Net;
using System.Net.Http.Json;
using Maliev.Aspire.Tests.Infrastructure;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.SupplyChain;

/// <summary>
/// Tests for supply chain domain workflows. Uses shared AspireTestFixture for performance.
/// </summary>
[Collection("AspireDomainTests")]
public class SupplyChainTests
{
    private readonly AspireTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupplyChainTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared test fixture.</param>
    /// <param name="output">The test output helper.</param>
    public SupplyChainTests(AspireTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Tests the full procurement workflow from supplier onboarding to purchase order creation.
    /// </summary>
    [Fact]
    public async Task Full_Procurement_Workflow_Succeeds()
    {
        // 1. Setup Clients
        var supplierClient = _fixture.CreateAuthenticatedClient("SupplierService");
        var materialClient = _fixture.CreateAuthenticatedClient("MaterialService");
        var poClient = _fixture.CreateAuthenticatedClient("PurchaseOrderService");
        var customer = await AspireTestData.CreateCorporateCustomerAsync(_fixture, "supply-chain");
        var customerId = customer.GetProperty("id").GetGuid();
        var order = await AspireTestData.CreateOrderAsync(_fixture, customerId, "Supply chain procurement workflow");
        var orderId = order.GetProperty("orderId").GetString()
            ?? throw new InvalidOperationException("OrderService did not return orderId.");

        // 2. Onboard Supplier
        _output.WriteLine("Scenario: Onboard Supplier");
        var createSupplierRequest = new
        {
            companyName = "TechSteel Inc",
            taxId = $"TAX-{Guid.NewGuid():N}"[..18],
            address = "123 Steel Road",
            city = "Bangkok",
            country = "USA",
            postalCode = "10110",
            capabilities = new[] { "CNC", "SheetMetal" },
            primaryContact = new
            {
                name = "Steel Contact",
                email = $"sales.{Guid.NewGuid():N}@techsteel.com",
                role = "Sales",
                phone = "+66812345678",
                isPrimary = true
            }
        };

        var supplierResponse = await supplierClient.PostAsJsonAsync("/supplier/v1/suppliers", createSupplierRequest);

        if (supplierResponse.StatusCode == HttpStatusCode.Forbidden)
        {
            var content = await supplierResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Forbidden detail: {content}");
        }

        Assert.Equal(HttpStatusCode.Created, supplierResponse.StatusCode);

        var supplier = await supplierResponse.Content.ReadFromJsonAsync<JsonElement>();
        var supplierId = supplier.GetProperty("id").GetGuid();
        _output.WriteLine($"✓ Supplier onboarded with ID: {supplierId}");

        // 3. Define Material
        _output.WriteLine("Scenario: Define Material");
        var createMaterialRequest = new
        {
            name = "Steel Rod",
            code = $"SKU-{Guid.NewGuid().ToString()[..8]}",
            pricePerUnit = 150.00m,
            stockLevel = 1000
        };

        var materialResponse = await materialClient.PostAsJsonAsync("/material/v1/materials", createMaterialRequest);
        Assert.Equal(HttpStatusCode.Created, materialResponse.StatusCode);

        var material = await materialResponse.Content.ReadFromJsonAsync<JsonElement>();
        var materialName = material.GetProperty("name").GetString();
        var materialCode = material.GetProperty("code").GetString();
        _output.WriteLine($"✓ Material defined: {materialName} ({materialCode})");

        // 4. Create Purchase Order
        _output.WriteLine("Scenario: Create Purchase Order");
        var createPoRequest = new
        {
            supplierID = 1,
            supplierServiceId = supplierId,
            orderID = 1,
            sourceOrderId = orderId,
            currencyID = 1,
            currencyCode = "THB",
            orderType = 1,
            whtRate = 0m,
            expectedDeliveryDate = DateTime.UtcNow.AddDays(14),
            shippingAddress = new
            {
                addressType = 0,
                contactName = "Warehouse",
                addressLine1 = "123 Test Road",
                city = "Bangkok",
                postalCode = "10110",
                country = "TH"
            },
            items = new[]
            {
                new
                {
                    externalOrderItemId = 1,
                    sourceOrderItemId = "primary",
                    quantity = 100m
                }
            }
        };

        var poResponse = await poClient.PostAsJsonAsync("/purchase-order/v1/purchase-orders", createPoRequest);
        var poContent = await poResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"PO response: {poResponse.StatusCode} - {poContent}");
        Assert.True(poResponse.StatusCode == HttpStatusCode.Created, $"Expected Created but got {poResponse.StatusCode}: {poContent}");

        var po = await poResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(supplierId, po.GetProperty("supplierServiceId").GetGuid());
        Assert.Single(po.GetProperty("items").EnumerateArray());
        _output.WriteLine($"✓ Purchase Order created: {po.GetProperty("orderNumber").GetString()}");
    }
}
