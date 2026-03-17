using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Foundation;

/// <summary>
/// Integration tests for the registry service.
/// </summary>
public class RegistryServiceTests(ITestOutputHelper output) : MalievTestBase(output)
{
    /// <summary>
    /// Tests that Thai locations autocomplete returns results.
    /// </summary>
    [Fact]
    public async Task ThaiLocations_Autocomplete_ReturnsResults()
    {
        var client = await CreateAuthenticatedClient("RegistryService");

        // Search for a common area in Bangkok
        var response = await client.GetAsync("/registry/v1/thai/addresses/autocomplete?query=Bangkok&limit=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(result.GetProperty("success").GetBoolean());
        var data = result.GetProperty("data");
        Assert.True(data.GetArrayLength() > 0, "Thai locations autocomplete should return results for 'Bangkok'");
    }

    /// <summary>
    /// Tests that Thai locations can be searched by postal code.
    /// </summary>
    [Fact]
    public async Task ThaiLocations_ByPostalCode_ReturnsResults()
    {
        var client = await CreateAuthenticatedClient("RegistryService");

        // 10110 is Sukhumvit area, Bangkok
        var response = await client.GetAsync("/registry/v1/thai/addresses/autocomplete-multi?postalCode=10110");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(result.GetProperty("success").GetBoolean());
        var data = result.GetProperty("data");
        Assert.True(data.GetArrayLength() > 0, "Thai locations should return results for postal code 10110");

        // Verify at least one result has the correct postal code
        bool found = false;
        foreach (var item in data.EnumerateArray())
        {
            if (item.GetProperty("postalCode").GetString() == "10110")
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "At least one result should match the searched postal code 10110");
    }
}
