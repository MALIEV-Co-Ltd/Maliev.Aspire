using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using Xunit;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Integration;

/// <summary>
/// Diagnostic tests to verify auth fix.
/// </summary>
[Collection("AspireDomainTests")]
public class RedirectDiagnosticTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Hit health endpoint to confirm service is up.
    /// </summary>
    [Fact]
    public async Task HealthEndpoint_IsReachable()
    {
        var client = _fixture.CreateClient("CustomerService");
        var response = await client.GetAsync("/customer/aspire-liveness");

        _output.WriteLine($"Base address: {client.BaseAddress}");
        _output.WriteLine($"Health status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Test authenticated request — if this passes, the auth fix works.
    /// </summary>
    [Fact]
    public async Task AuthenticatedRequest_ShouldNotBe401()
    {
        var client = _fixture.CreateAuthenticatedClient("CustomerService");
        var response = await client.GetAsync("/customer/v1/customers");

        _output.WriteLine($"Status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Body: {body[..Math.Min(500, body.Length)]}");
        }

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Raw Aspire client (no HTTPS fix) should get 401 due to redirect stripping auth.
    /// </summary>
    [Fact]
    public async Task RawAspireClient_StillGets401()
    {
        var token = _fixture.GetAdminToken();
        var client = _fixture.AppFactory!.CreateHttpClient("CustomerService");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/customer/v1/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
