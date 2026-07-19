using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Commercial;

/// <summary>
/// Integration tests for delivery workflow.
/// </summary>
[Collection("AspireDomainTests")]
public class DeliveryWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests the full delivery workflow from creation to delivery completion.
    /// </summary>
    [Fact]
    public async Task FullDeliveryWorkflow_CreateAndUpdateStatus()
    {
        var deliveryClient = _fixture.CreateAuthenticatedClient("DeliveryService");

        // 1. Create Customer
        var customer = await AspireTestData.CreateCustomerAsync(_fixture, "delivery");
        var customerId = customer.GetProperty("id").GetGuid();
        var customerName = customer.GetProperty("name").GetString();

        // 2. Create Delivery Note
        var createRequest = new
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            CustomerName = customerName,
            DeliveryDate = DateTime.UtcNow.AddDays(1),
            Items = new[]
            {
                new
                {
                    ProductCode = "DELIVERY-TEST",
                    ProductName = "Delivery Test Product",
                    QuantityOrdered = 10m,
                    QuantityManufactured = 10m,
                    QuantityDelivered = 10m,
                    UnitOfMeasure = "pcs"
                }
            }
        };

        var response = await deliveryClient.PostAsJsonAsync("/delivery/v1/delivery-notes", createRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dn = await response.Content.ReadFromJsonAsync<JsonElement>();
        var dnId = dn.GetProperty("deliveryNoteId").GetString();
        _output.WriteLine($"Delivery Note created: {dnId}");

        // 3. Update status to InTransit
        var transitRequest = new { NewStatus = "InTransit" };
        var transitResponse = await deliveryClient.PatchAsJsonAsync($"/delivery/v1/delivery-notes/{dnId}/status", transitRequest);
        Assert.Equal(HttpStatusCode.OK, transitResponse.StatusCode);

        // 4. Update status to Delivered
        var deliveredRequest = new
        {
            NewStatus = "Delivered",
            ActualDeliveryTime = DateTime.UtcNow,
            ReceivedByName = "Test Receiver"
        };
        var deliveredResponse = await deliveryClient.PatchAsJsonAsync($"/delivery/v1/delivery-notes/{dnId}/status", deliveredRequest);
        Assert.Equal(HttpStatusCode.OK, deliveredResponse.StatusCode);

        // 5. Verify final status
        var finalResponse = await deliveryClient.GetAsync($"/delivery/v1/delivery-notes/{dnId}");
        var finalDn = await finalResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Delivered", finalDn.GetProperty("status").GetString());
    }
}
