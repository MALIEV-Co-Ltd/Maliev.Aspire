using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Foundation;

/// <summary>
/// Integration tests for the authentication workflow.
/// </summary>
[Collection("AspireDomainTests")]
public class AuthWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests that a valid @maliev.com email can exchange for an access token.
    /// </summary>
    [Fact]
    public async Task GoogleExchange_WithValidEmail_ReturnsAccessToken()
    {
        _output.WriteLine("Testing Google Exchange with valid @maliev.com email...");
        var client = _fixture.CreateClient("AuthService");

        var request = new
        {
            Email = "test.user@maliev.com",
            FullName = "Test User"
        };

        var response = await client.PostAsJsonAsync("/auth/v1/exchange/google", request);

        _output.WriteLine($"Response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("access_token", out _));
    }

    /// <summary>
    /// Tests that a non-company email is rejected.
    /// </summary>
    [Fact]
    public async Task GoogleExchange_WithNonCompanyEmail_ReturnsForbidden()
    {
        _output.WriteLine("Testing Google Exchange with invalid domain email...");
        var client = _fixture.CreateClient("AuthService");

        var request = new
        {
            Email = "intruder@gmail.com",
            FullName = "Intruder"
        };

        var response = await client.PostAsJsonAsync("/auth/v1/exchange/google", request);

        _output.WriteLine($"Response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
