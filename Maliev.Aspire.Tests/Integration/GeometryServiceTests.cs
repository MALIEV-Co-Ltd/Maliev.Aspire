using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Integration;

/// <summary>
/// Integration tests for GeometryService health and liveness.
/// Verifies that the Python/FastAPI service starts correctly and responds to health checks.
/// Requires Docker to be running for the GeometryService container.
/// </summary>
[Collection("AspireDomainTests")]
[Trait("Category", "Slow")]
[Trait("RequiresDocker", "true")]
public class GeometryServiceTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Verifies that the GeometryService /aspire-liveness endpoint returns 200 OK.
    /// This test validates that the Python FastAPI service starts successfully
    /// and can handle HTTP requests.
    /// </summary>
    [Fact]
    public async Task GeometryService_Liveness_ReturnsOk()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient("GeometryService");

        // Act
        _output.WriteLine("[GeometryServiceTests] Calling GET /geometry/aspire-liveness");
        var response = await client.GetAsync("/geometry/aspire-liveness");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _output.WriteLine($"[GeometryServiceTests] Liveness check passed: {response.StatusCode}");
    }

    /// <summary>
    /// Verifies that the GeometryService OpenAPI endpoint is accessible.
    /// The Scalar documentation endpoint should return a 200 response.
    /// </summary>
    [Fact]
    public async Task GeometryService_Documentation_ReturnsOk()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient("GeometryService");

        // Act
        _output.WriteLine("[GeometryServiceTests] Calling GET /geometry/scalar");
        var response = await client.GetAsync("/geometry/scalar");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _output.WriteLine($"[GeometryServiceTests] Documentation endpoint passed: {response.StatusCode}");
    }

    /// <summary>
    /// Verifies that GeometryService protects non-public endpoints before routing.
    /// </summary>
    [Fact]
    public async Task GeometryService_NonExistentEndpoint_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient("GeometryService");

        // Act
        _output.WriteLine("[GeometryServiceTests] Calling GET /geometry/non-existent");
        var response = await client.GetAsync("/geometry/non-existent");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine($"[GeometryServiceTests] authorization check passed: {response.StatusCode}");
    }
}
