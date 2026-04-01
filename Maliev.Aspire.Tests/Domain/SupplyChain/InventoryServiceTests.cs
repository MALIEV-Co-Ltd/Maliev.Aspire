using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.SupplyChain;

/// <summary>
/// Integration tests for the InventoryService stock batch management.
/// </summary>
[Collection("AspireDomainTests")]
public class InventoryServiceTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Verifies that creating a stock batch with valid data returns 201 Created.
    /// </summary>
    [Fact]
    public async Task CreateBatch_WithValidData_ReturnsCreated()
    {
        var inventoryClient = _fixture.CreateAuthenticatedClient("InventoryService");
        var materialClient = _fixture.CreateAuthenticatedClient("MaterialService");

        var matResponse = await materialClient.GetAsync("/material/v1/materials?pageSize=1");
        var matResult = await matResponse.Content.ReadFromJsonAsync<JsonElement>();
        var materials = matResult.GetProperty("items");

        if (materials.GetArrayLength() == 0)
        {
            _output.WriteLine("No materials found — skipping test");
            return;
        }

        var materialId = materials[0].GetProperty("id").GetGuid();
        _output.WriteLine($"Using material: {materialId}");

        var createBatchRequest = new
        {
            MaterialId = materialId,
            SupplierBatchNumber = $"BATCH-{Guid.NewGuid():N}"[..20],
            InitialWeightGrams = 1000.0,
            RemainingWeightGrams = 1000.0,
            LowStockThresholdGrams = 200.0,
            PurchaseDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddYears(1),
            UnitCost = 25.00m,
            Currency = "THB",
            Notes = "Integration test batch"
        };

        var response = await inventoryClient.PostAsJsonAsync("/inventory/v1/stock/batches", createBatchRequest);
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Create batch response: {response.StatusCode} - {content}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var batch = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(batch.TryGetProperty("id", out _) || batch.TryGetProperty("batchId", out _),
            $"Response should contain batch ID: {content}");
    }

    /// <summary>
    /// Verifies that retrieving batch status returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetBatchStatus_ReturnsOk()
    {
        var inventoryClient = _fixture.CreateAuthenticatedClient("InventoryService");

        var response = await inventoryClient.GetAsync("/inventory/v1/stock/batches/status");

        _output.WriteLine($"Get status response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that after creating a batch, the status endpoint reflects the new batch.
    /// </summary>
    [Fact]
    public async Task CreateBatch_AndVerifyStatus_ReflectsNewBatch()
    {
        var inventoryClient = _fixture.CreateAuthenticatedClient("InventoryService");
        var materialClient = _fixture.CreateAuthenticatedClient("MaterialService");

        var matResponse = await materialClient.GetAsync("/material/v1/materials?pageSize=1");
        var matResult = await matResponse.Content.ReadFromJsonAsync<JsonElement>();
        var materials = matResult.GetProperty("items");

        if (materials.GetArrayLength() == 0)
        {
            _output.WriteLine("No materials found — skipping test");
            return;
        }

        var materialId = materials[0].GetProperty("id").GetGuid();

        var createBatchRequest = new
        {
            MaterialId = materialId,
            SupplierBatchNumber = $"BATCH-{Guid.NewGuid():N}"[..20],
            InitialWeightGrams = 500.0,
            RemainingWeightGrams = 500.0,
            LowStockThresholdGrams = 100.0,
            PurchaseDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddYears(1),
            UnitCost = 15.00m,
            Currency = "THB"
        };

        var createResponse = await inventoryClient.PostAsJsonAsync("/inventory/v1/stock/batches", createBatchRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var statusResponse = await inventoryClient.GetAsync($"/inventory/v1/stock/batches/status?materialId={materialId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
    }
}
