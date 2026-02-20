using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Commercial;

public class DeliveryWorkflowTests(ITestOutputHelper output) : MalievTestBase(output)
{
    [Fact]
    public async Task FullDeliveryWorkflow_CreateAndUpdateStatus()
    {
        var customerClient = await CreateAuthenticatedClient("CustomerService");
        var deliveryClient = await CreateAuthenticatedClient("DeliveryService");

        // 1. Create Customer
        var createCustomerRequest = new
        {
            FirstName = "Delivery",
            LastName = "Test",
            Email = $"delivery.test.{Guid.NewGuid():N}@example.com",
            Type = "Corporate",
            TaxId = "8888888888888"
        };
        var custResponse = await customerClient.PostAsJsonAsync("/customer/v1/customers", createCustomerRequest);
        var customer = await custResponse.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = customer.GetProperty("id").GetGuid();

        // 2. Create Delivery Note
        var createRequest = new
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            DeliveryDate = DateTime.UtcNow.AddDays(1),
            Items = new[]
            {
                new
                {
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
        Output.WriteLine($"Delivery Note created: {dnId}");

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
