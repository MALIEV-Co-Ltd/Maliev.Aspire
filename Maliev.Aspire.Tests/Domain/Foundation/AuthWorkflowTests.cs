using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Foundation;

public class AuthWorkflowTests(ITestOutputHelper output) : MalievTestBase(output)
{
    [Fact]
    public async Task GoogleExchange_WithValidEmail_ReturnsAccessToken()
    {
        Output.WriteLine("Testing Google Exchange with valid @maliev.com email...");
        var client = CreateClient("AuthService");

        var request = new
        {
            Email = "test.user@maliev.com",
            FullName = "Test User"
        };

        var response = await client.PostAsJsonAsync("/auth/v1/exchange/google", request);

        Output.WriteLine($"Response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task GoogleExchange_WithNonCompanyEmail_ReturnsForbidden()
    {
        Output.WriteLine("Testing Google Exchange with invalid domain email...");
        var client = CreateClient("AuthService");

        var request = new
        {
            Email = "intruder@gmail.com",
            FullName = "Intruder"
        };

        var response = await client.PostAsJsonAsync("/auth/v1/exchange/google", request);

        Output.WriteLine($"Response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
