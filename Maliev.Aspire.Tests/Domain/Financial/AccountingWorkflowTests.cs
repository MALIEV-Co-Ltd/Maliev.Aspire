using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Financial;

/// <summary>
/// Integration tests for the accounting workflow.
/// </summary>
public class AccountingWorkflowTests(ITestOutputHelper output) : MalievTestBase(output)
{
    /// <summary>
    /// Tests that the chart of accounts returns seeded data.
    /// </summary>
    [Fact]
    public async Task GetChartOfAccounts_ReturnsSeededData()
    {
        var client = await CreateAuthenticatedClient("AccountingService");

        var response = await client.GetAsync("/accounting/v1/chart-of-accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(result);
        Assert.True(result.Count > 0, "Chart of accounts should be seeded.");
        Output.WriteLine($"Found {result.Count} accounts in CoA.");
    }

    /// <summary>
    /// Tests that a balanced journal entry can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateJournalEntry_WithBalancedLines_Succeeds()
    {
        var client = await CreateAuthenticatedClient("AccountingService");

        // 1. Get two accounts to use
        var coaResponse = await client.GetAsync("/accounting/v1/chart-of-accounts");
        var accounts = await coaResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        var acc1 = accounts![0].GetProperty("id").GetGuid();
        var acc2 = accounts![1].GetProperty("id").GetGuid();

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
