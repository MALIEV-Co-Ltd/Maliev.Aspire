using System.Net;
using System.Net.Http.Json;
using Maliev.Aspire.Tests.Infrastructure;
using Maliev.Intranet.Shared;
using Maliev.Intranet.Shared.Dtos;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.SupplyChain;

/// <summary>
/// Tests for supply chain domain workflows. Uses shared AspireTestFixture for performance.
/// </summary>
[Collection("AspireDomainTests")]
public class SupplyChainTests : IClassFixture<AspireTestFixture>
{
    private readonly AspireTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SupplyChainTests(AspireTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Full_Procurement_Workflow_Succeeds()
    {
        // 1. Setup Clients
        var supplierClient = _fixture.CreateAuthenticatedClient("SupplierService");
        var materialClient = _fixture.CreateAuthenticatedClient("MaterialService");
        var poClient = _fixture.CreateAuthenticatedClient("PurchaseOrderService");

        // 2. Onboard Supplier
        _output.WriteLine("Scenario: Onboard Supplier");
        var createSupplierRequest = new
        {
            name = "TechSteel Inc",
            email = $"sales.{Guid.NewGuid()}@techsteel.com",
            country = "USA",
            status = "Active"
        };

        var supplierResponse = await supplierClient.PostAsJsonAsync("/supplier/v1/suppliers", createSupplierRequest);

        if (supplierResponse.StatusCode == HttpStatusCode.Forbidden)
        {
            var content = await supplierResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Forbidden detail: {content}");
        }

        Assert.Equal(HttpStatusCode.Created, supplierResponse.StatusCode);

        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierSummaryDto>();
        Assert.NotNull(supplier);
        var supplierId = supplier.Id;
        _output.WriteLine($"✓ Supplier onboarded with ID: {supplierId}");

        // 3. Define Material
        _output.WriteLine("Scenario: Define Material");
        var createMaterialRequest = new
        {
            name = "Steel Rod",
            sku = $"SKU-{Guid.NewGuid().ToString()[..8]}",
            category = "Raw Materials",
            unitPrice = 150.00m,
            unit = "pcs"
        };

        var materialResponse = await materialClient.PostAsJsonAsync("/material/v1/materials", createMaterialRequest);
        Assert.Equal(HttpStatusCode.Created, materialResponse.StatusCode);

        var material = await materialResponse.Content.ReadFromJsonAsync<MaterialSummaryDto>();
        Assert.NotNull(material);
        _output.WriteLine($"✓ Material defined: {material.Name} ({material.SKU})");

        // 4. Create Purchase Order
        _output.WriteLine("Scenario: Create Purchase Order");
        var createPoRequest = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            Date = DateTime.UtcNow,
            Items =
            [
                new PurchaseOrderLineItemDto
                {
                    MaterialId = material.Id,
                    Quantity = 100,
                    UnitPrice = 150.00m
                }
            ]
        };

        var poResponse = await poClient.PostAsJsonAsync("/purchase-order/v1/orders", createPoRequest);
        Assert.Equal(HttpStatusCode.Created, poResponse.StatusCode);

        var po = await poResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        Assert.NotNull(po);
        Assert.Equal(supplierId, po.SupplierId);
        Assert.Single(po.Items);
        _output.WriteLine($"✓ Purchase Order created: {po.PoNumber}");
    }
}
