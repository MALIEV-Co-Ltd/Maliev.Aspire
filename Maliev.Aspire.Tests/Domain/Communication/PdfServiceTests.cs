using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Communication;

public class PdfServiceTests(ITestOutputHelper output) : MalievTestBase(output)
{
    [Fact]
    public async Task GenerateInvoicePdf_ReturnsStorageUrl()
    {
        var client = await CreateAuthenticatedClient("PdfService");

        // 1. Get available templates
        var templatesResponse = await client.GetAsync("/pdf/v1/templates");
        Assert.Equal(HttpStatusCode.OK, templatesResponse.StatusCode);
        var templates = await templatesResponse.Content.ReadFromJsonAsync<List<JsonElement>>();

        Assert.True(templates != null && templates.Count > 0,
            "No PDF templates found — ensure the database seeder has run before executing integration tests.");

        var templateCode = templates[0].GetProperty("code").GetString()!;
        Output.WriteLine($"Using template: {templateCode}");

        // 2. Request PDF generation
        var request = new
        {
            TemplateCode = templateCode,
            ReferenceId = Guid.NewGuid().ToString(),
            DocumentType = 1, // Invoice
            Data = new
            {
                InvoiceNumber = "INV-2026-0001",
                CustomerName = "Test Customer",
                TotalAmount = 1070.00m,
                Currency = "THB",
                Lines = new[]
                {
                    new { Description = "Item 1", Quantity = 1, UnitPrice = 1000.00m, TotalPrice = 1000.00m }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/pdf/v1/generations/generate", request);

        // It might return 500 if dependencies (UploadService) are not fully ready in test environment
        // But we expect 200 OK if everything is fine.
        Output.WriteLine($"Generation Response: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("storageUrl", out _), "Response missing 'storageUrl' property");
        var url = result.GetProperty("storageUrl").GetString();
        Assert.False(string.IsNullOrEmpty(url), "storageUrl must not be empty");
        Output.WriteLine($"PDF Generated at: {url}");
    }
}
