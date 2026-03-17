using System.Net;
using System.Net.Http.Json;
using Maliev.Aspire.Tests.Infrastructure;
using Maliev.Intranet.Shared;
using Maliev.Intranet.Shared.Dtos;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Financial;

/// <summary>
/// Integration tests for the invoice workflow.
/// </summary>
public class InvoiceWorkflowTests(ITestOutputHelper output) : MalievTestBase(output)
{
    /// <summary>
    /// Tests the invoice lifecycle including creation and splitting.
    /// </summary>
    [Fact]
    public async Task Invoice_Lifecycle_And_Splitting_Succeeds()
    {
        // 1. Setup Clients
        var invoiceClient = await CreateAuthenticatedClient("InvoiceService");
        var customerId = Guid.NewGuid(); // Simplified for test scope

        // 2. Create Draft Invoice
        Output.WriteLine("Scenario: Create Draft Invoice");
        var createRequest = new CreateInvoiceRequest
        {
            CustomerId = customerId,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            PaymentTermsDays = 30,
            Currency = "THB",
            Items =
            [
                new InvoiceItemDto
                {
                    Description = "3D Printing Service",
                    Quantity = 10,
                    UnitPrice = 1000m,
                    TaxRate = 7
                }
            ]
        };

        var response = await invoiceClient.PostAsJsonAsync("/invoice/v1/invoices", createRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceSummaryDto>();
        Assert.NotNull(invoice);
        Output.WriteLine($"✓ Invoice created: {invoice.InvoiceNumber} (Status: {invoice.Status})");

        // 3. Split Invoice
        Output.WriteLine("Scenario: Split Invoice");
        var splitRequest = new SplitInvoiceRequest
        {
            Reason = "Customer requested payment plan",
            Splits =
            [
                new InvoiceSplitDetail { Percentage = 50, DueDate = DateTime.UtcNow.AddDays(15), Amount = invoice.Total / 2 },
                new InvoiceSplitDetail { Percentage = 50, DueDate = DateTime.UtcNow.AddDays(30), Amount = invoice.Total / 2 }
            ]
        };

        var splitResponse = await invoiceClient.PostAsJsonAsync($"/invoice/v1/invoices/{invoice.Id}/split", splitRequest);
        Assert.Equal(HttpStatusCode.OK, splitResponse.StatusCode);
        Output.WriteLine("✓ Invoice split successfully");

        // 4. Verify Parent Status
        var updatedInvoice = await invoiceClient.GetFromJsonAsync<InvoiceDetailDto>($"/invoice/v1/invoices/{invoice.Id}");
        Assert.NotNull(updatedInvoice);
        Assert.Equal("Split", updatedInvoice.Status);
        Assert.Equal(2, updatedInvoice.ChildInvoices.Count);
        Output.WriteLine("✓ Parent invoice status updated to 'Split' and children linked");
    }

    /// <summary>
    /// Tests the billing note lifecycle.
    /// </summary>
    [Fact]
    public async Task BillingNote_Lifecycle_Succeeds()
    {
        var invoiceClient = await CreateAuthenticatedClient("InvoiceService");
        var customerId = Guid.NewGuid();

        // 1. Create 2 Invoices
        var inv1 = await CreateTestInvoice(invoiceClient, customerId);
        var inv2 = await CreateTestInvoice(invoiceClient, customerId);

        // 2. Create Billing Note
        Output.WriteLine("Scenario: Create Billing Note");
        var createBnRequest = new
        {
            customerId = customerId,
            issueDate = DateTime.UtcNow,
            dueDate = DateTime.UtcNow.AddDays(7),
            invoiceIds = new[] { inv1.Id, inv2.Id },
            notes = "Monthly billing"
        };

        var response = await invoiceClient.PostAsJsonAsync("/invoice/v1/billing-notes", createBnRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Output.WriteLine("✓ Billing Note created for multiple invoices");
    }

    private async Task<InvoiceSummaryDto> CreateTestInvoice(HttpClient client, Guid customerId)
    {
        var request = new CreateInvoiceRequest
        {
            CustomerId = customerId,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Items = [new InvoiceItemDto { Description = "Test Item", Quantity = 1, UnitPrice = 100 }]
        };
        var response = await client.PostAsJsonAsync("/invoice/v1/invoices", request);
        return (await response.Content.ReadFromJsonAsync<InvoiceSummaryDto>())!;
    }
}
