using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Financial;

/// <summary>
/// Integration tests for the accounting workflow.
/// </summary>
[Collection("AspireDomainTests")]
public class AccountingWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;
    /// <summary>
    /// Tests that the chart of accounts returns created data.
    /// </summary>
    [Fact]
    public async Task GetChartOfAccounts_ReturnsCreatedData()
    {
        var client = _fixture.CreateAuthenticatedClient("AccountingService");
        await AspireTestData.CreateChartAccountAsync(_fixture, "Asset", "Cash");

        var response = await client.GetAsync("/accounting/v1/chart-of-accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(result);
        Assert.True(result.Count > 0, "Chart of accounts should include created accounts.");
        _output.WriteLine($"Found {result.Count} accounts in CoA.");
    }

    /// <summary>
    /// Tests that a balanced journal entry can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateJournalEntry_WithBalancedLines_Succeeds()
    {
        var client = _fixture.CreateAuthenticatedClient("AccountingService");

        // 1. Create two accounts to use
        var debitAccount = await AspireTestData.CreateChartAccountAsync(_fixture, "Asset", "Integration Debit");
        var creditAccount = await AspireTestData.CreateChartAccountAsync(_fixture, "Revenue", "Integration Credit");
        var acc1 = debitAccount.GetProperty("id").GetGuid();
        var acc2 = creditAccount.GetProperty("id").GetGuid();

        // 2. Create balanced journal entry
        var request = new
        {
            EntryDate = DateTime.UtcNow,
            Description = "Integration Test Entry",
            Lines = new[]
            {
                new
                {
                    AccountId = acc1,
                    DebitAmount = 1000.00m,
                    CreditAmount = 0m,
                    Description = "Debit line"
                },
                new
                {
                    AccountId = acc2,
                    DebitAmount = 0m,
                    CreditAmount = 1000.00m,
                    Description = "Credit line"
                }
            }
        };

        var response = await client.PostAsJsonAsync("/accounting/v1/journal-entries", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1000.00m, result.GetProperty("totalDebit").GetDecimal());
        Assert.Equal(1000.00m, result.GetProperty("totalCredit").GetDecimal());
    }
}
