using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Foundation;

/// <summary>
/// Integration tests for the country service.
/// </summary>
public class CountryServiceTests(ITestOutputHelper output) : MalievTestBase(output)
{
    /// <summary>
    /// Tests that the countries endpoint returns a non-empty list.
    /// </summary>
    [Fact]
    public async Task GetCountries_ReturnsNonEmptyList()
    {
        var client = await CreateAuthenticatedClient("CountryService");

        var response = await client.GetAsync("/country/v1/countries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // PaginatedResponse<CountryResponse> has a "data" property
        var data = result.GetProperty("data");
        Assert.True(data.GetArrayLength() > 0, "Country list should not be empty after seeding.");
    }

    /// <summary>
    /// Tests that a country can be retrieved by its ID.
    /// </summary>
    [Fact]
    public async Task GetCountryById_ReturnsCorrectCountry()
    {
        var client = await CreateAuthenticatedClient("CountryService");

        // 1. Get a country to find its ID
        var listResponse = await client.GetAsync("/country/v1/countries");
        var listResult = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstCountry = listResult.GetProperty("data")[0];
        var id = firstCountry.GetProperty("id").GetGuid();
        var expectedName = firstCountry.GetProperty("name").GetString();

        // 2. Get by ID
        var response = await client.GetAsync($"/country/v1/countries/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, result.GetProperty("id").GetGuid());
        Assert.Equal(expectedName, result.GetProperty("name").GetString());
    }

    /// <summary>
    /// Tests that a country can be retrieved by its ISO 2 code.
    /// </summary>
    [Fact]
    public async Task GetByIso2_ReturnsCorrectCountry()
    {
        var client = await CreateAuthenticatedClient("CountryService");

        // Thailand ISO2 is TH
        var response = await client.GetAsync("/country/v1/countries/iso2/TH");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Thailand", result.GetProperty("name").GetString());
        Assert.Equal("TH", result.GetProperty("iso2").GetString());
    }
}
