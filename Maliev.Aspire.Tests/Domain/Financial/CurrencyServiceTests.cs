using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Financial;

/// <summary>
/// Integration tests for the currency service.
/// </summary>
public class CurrencyServiceTests(ITestOutputHelper output) : MalievTestBase(output)
{
    /// <summary>
    /// Tests that the currencies endpoint returns the seeded currency list.
    /// </summary>
    [Fact]
    public async Task GetCurrencies_ReturnsSeededList()
    {
        var client = await CreateAuthenticatedClient("CurrencyService");

        var response = await client.GetAsync("/currency/v1/currencies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // PaginatedCurrencyResponse has a "data" property (based on CurrenciesController.cs context)
        var data = result.GetProperty("items"); // Usually "items" or "data" in paginated responses
        var items = data.EnumerateArray().ToList();

        Assert.True(items.Any(c => c.GetProperty("code").GetString() == "THB"), "THB should exist in seeded data.");
        Assert.True(items.Any(c => c.GetProperty("code").GetString() == "USD"), "USD should exist in seeded data.");
        Output.WriteLine("Verified THB and USD exist in currency list.");
    }

    /// <summary>
    /// Tests that currency can be retrieved by country code.
    /// </summary>
    [Fact]
    public async Task GetCurrencyByCountry_ReturnsCorrectCurrency()
    {
        var client = await CreateAuthenticatedClient("CurrencyService");

        // Resolve for Thailand (TH)
        var response = await client.GetAsync("/currency/v1/currencies/by-country?iso=TH");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("THB", result.GetProperty("code").GetString());
        Output.WriteLine("Verified TH country resolves to THB currency.");
    }
}
