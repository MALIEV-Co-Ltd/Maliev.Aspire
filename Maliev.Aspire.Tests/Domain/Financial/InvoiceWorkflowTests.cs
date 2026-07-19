using System.Net;
using System.Net.Http.Json;
using Maliev.Aspire.Tests.Infrastructure;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Financial;

/// <summary>
/// Integration tests for the invoice workflow.
/// </summary>
[Collection("AspireDomainTests")]
public class InvoiceWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;
    /// <summary>
    /// Tests the invoice lifecycle including creation and splitting.
    /// </summary>
    [Fact]
    public async Task Invoice_Lifecycle_And_Splitting_Succeeds()
    {
        // 1. Setup Clients
        var invoiceClient = _fixture.CreateAuthenticatedClient("InvoiceService");

        // 2. Create Draft Invoice
        _output.WriteLine("Scenario: Create Draft Invoice");
        var invoice = await AspireTestData.CreateInvoiceAsync(_fixture);
        var invoiceId = invoice.GetProperty("id").GetGuid();
        _output.WriteLine($"✓ Invoice created: {invoiceId} (Status: {invoice.GetProperty("status").GetString()})");

        var finalized = await AspireTestData.FinalizeInvoiceAsync(_fixture, invoiceId);
        Assert.Equal("Finalized", finalized.GetProperty("status").GetString());

        // 3. Split Invoice
        _output.WriteLine("Scenario: Split Invoice");
        var splitRequest = new
        {
            Reason = "Customer requested payment plan",
            SplitRules = new[]
            {
                new { Percentage = 50m, Notes = "First half" },
                new { Percentage = 50m, Notes = "Second half" }
            }
        };

        var splitResponse = await invoiceClient.PostAsJsonAsync($"/invoice/v1/invoices/{invoiceId}/split", splitRequest);
        Assert.Equal(HttpStatusCode.Created, splitResponse.StatusCode);
        _output.WriteLine("✓ Invoice split successfully");

        // 4. Verify Parent Status
        var updatedInvoice = await invoiceClient.GetFromJsonAsync<JsonElement>($"/invoice/v1/invoices/{invoiceId}");
        Assert.Equal("Split", updatedInvoice.GetProperty("status").GetString());
        Assert.Equal(2, updatedInvoice.GetProperty("childInvoiceSummaries").GetArrayLength());
        _output.WriteLine("✓ Parent invoice status updated to 'Split' and children linked");
    }

    /// <summary>
    /// Tests the billing note lifecycle.
    /// </summary>
    [Fact]
    public async Task BillingNote_Lifecycle_Succeeds()
    {
        var invoiceClient = _fixture.CreateAuthenticatedClient("InvoiceService");
        // 1. Create 2 Invoices
        var customer = await AspireTestData.CreateCorporateCustomerAsync(_fixture, "billing");
        var customerId = customer.GetProperty("id").GetGuid();
        var customerName = customer.GetProperty("name").GetString();
        var inv1 = await CreateTestInvoice(customerId, customerName);
        var inv2 = await CreateTestInvoice(customerId, customerName);

        // 2. Create Billing Note
        _output.WriteLine("Scenario: Create Billing Note");
        var createBnRequest = new
        {
            customerId = customerId,
            issueDate = DateTime.UtcNow,
            dueDate = DateTime.UtcNow.AddDays(7),
            invoiceIds = new[] { inv1.GetProperty("id").GetGuid(), inv2.GetProperty("id").GetGuid() },
            notes = "Monthly billing"
        };

        var response = await invoiceClient.PostAsJsonAsync("/invoice/v1/billing-notes", createBnRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        _output.WriteLine("✓ Billing Note created for multiple invoices");
    }

    private async Task<JsonElement> CreateTestInvoice(Guid customerId, string? customerName)
    {
        var invoice = await AspireTestData.CreateInvoiceAsync(_fixture, customerId, customerName);
        return await AspireTestData.FinalizeInvoiceAsync(_fixture, invoice.GetProperty("id").GetGuid());
    }
}
