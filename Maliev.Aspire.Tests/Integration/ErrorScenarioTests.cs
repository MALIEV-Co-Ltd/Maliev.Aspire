using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Integration;

/// <summary>
/// Integration tests verifying error handling scenarios across services.
/// </summary>
[Collection("AspireDomainTests")]
public class ErrorScenarioTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Verifies that accessing the customer endpoint without authentication returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task UnauthorizedAccess_CustomerEndpoint_Returns401()
    {
        var client = _fixture.CreateClient("CustomerService");

        var response = await client.GetAsync("/customer/v1/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies that accessing the order endpoint without authentication returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task UnauthorizedAccess_OrderEndpoint_Returns401()
    {
        var client = _fixture.CreateClient("OrderService");

        var response = await client.GetAsync("/order/v1/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies that accessing the invoice endpoint without authentication returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task UnauthorizedAccess_InvoiceEndpoint_Returns401()
    {
        var client = _fixture.CreateClient("InvoiceService");

        var response = await client.GetAsync("/invoice/v1/invoices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies that requesting a non-existent customer by ID returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task NonExistentCustomer_Returns404()
    {
        var client = _fixture.CreateAuthenticatedClient("CustomerService");
        var fakeId = Guid.NewGuid();

        var response = await client.GetAsync($"/customer/v1/customers/{fakeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies that requesting a non-existent order by ID returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task NonExistentOrder_Returns404()
    {
        var client = _fixture.CreateAuthenticatedClient("OrderService");
        var fakeId = Guid.NewGuid();

        var response = await client.GetAsync($"/order/v1/orders/{fakeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies that requesting a non-existent invoice by ID returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task NonExistentInvoice_Returns404()
    {
        var client = _fixture.CreateAuthenticatedClient("InvoiceService");
        var fakeId = Guid.NewGuid();

        var response = await client.GetAsync($"/invoice/v1/invoices/{fakeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies that sending a malformed customer creation request returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task MalformedCustomerRequest_Returns400()
    {
        var client = _fixture.CreateAuthenticatedClient("CustomerService");

        var response = await client.PostAsJsonAsync("/customer/v1/customers", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies that sending a malformed invoice creation request returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task MalformedInvoiceRequest_Returns400()
    {
        var client = _fixture.CreateAuthenticatedClient("InvoiceService");

        var response = await client.PostAsJsonAsync("/invoice/v1/invoices", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies that sending a malformed delivery note creation request returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task MalformedDeliveryNoteRequest_Returns400()
    {
        var client = _fixture.CreateAuthenticatedClient("DeliveryService");

        var response = await client.PostAsJsonAsync("/delivery/v1/delivery-notes", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies that sending a malformed leave request returns an appropriate error status.
    /// </summary>
    [Fact]
    public async Task MalformedLeaveRequest_Returns400()
    {
        var client = _fixture.CreateAuthenticatedClient("LeaveService");
        var fakeEmployeeId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync($"/leave/v1/LeaveRequests/{fakeEmployeeId}", new { });

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.UnprocessableContent,
            $"Expected 400/404/422 but got {response.StatusCode}");
    }
}
