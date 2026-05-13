using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.SupplyChain;

/// <summary>
/// Tests for pricing service. Uses shared AspireTestFixture for performance.
/// </summary>
[Collection("AspireDomainTests")]
public class PricingServiceTests : IClassFixture<AspireTestFixture>
{
    private readonly AspireTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricingServiceTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared test fixture.</param>
    /// <param name="output">The test output helper.</param>
    public PricingServiceTests(AspireTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Tests that the pricing service returns a positive price.
    /// </summary>
    [Fact]
    public async Task CalculatePrice_ReturnsPositiveAmount()
    {
        var client = _fixture.CreateAuthenticatedClient("PricingService");

        var request = new
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.Parse("8068f25e-0b7b-8a55-9b6c-1345e58ed9b4"),
            MaterialCode = "M-PLA-001",
            ManufacturingProcessId = Guid.Parse("5ef73574-8f61-5d53-8073-5c344bfd22ca"),
            ManufacturingProcessName = "FDM",
            Quantity = 10,
            Geometry = new
            {
                VolumeCm3 = 25.5m,
                SupportVolumeCm3 = 5.2m,
                SurfaceAreaCm2 = 120.0m,
                BoundingBoxX = 50.0m,
                BoundingBoxY = 50.0m,
                BoundingBoxZ = 20.0m,
                IsManifold = true,
                TriangleCount = 1500
            }
        };

        var response = await client.PostAsJsonAsync("/pricing/v1/calculate", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        var totalPrice = result.GetProperty("totalAmount").GetDecimal();
        _output.WriteLine($"Calculated Price: {totalPrice}");
        Assert.True(totalPrice > 0, "Calculated price should be positive.");
    }
}
