using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Commercial;

/// <summary>
/// Integration tests for the JobService shop floor management endpoints.
/// </summary>
[Collection("AspireDomainTests")]
public class JobServiceTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Verifies that retrieving all jobs returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetJobs_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("JobService");

        var response = await client.GetAsync("/job/v1/jobs");

        _output.WriteLine($"GetJobs response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that retrieving the Kanban board view returns 200 OK with valid JSON.
    /// </summary>
    [Fact]
    public async Task GetKanban_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("JobService");

        var response = await client.GetAsync("/job/v1/jobs/kanban");

        _output.WriteLine($"GetKanban response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.ValueKind == JsonValueKind.Array || result.ValueKind == JsonValueKind.Object,
            $"Expected array or object but got {result.ValueKind}");
    }

    /// <summary>
    /// Verifies that retrieving the queue depth returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetQueueDepth_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("JobService");

        var response = await client.GetAsync("/job/v1/jobs/queue-depth");

        _output.WriteLine($"GetQueueDepth response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that requesting a non-existent job by ID returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetJob_ByNonExistentId_Returns404()
    {
        var client = _fixture.CreateAuthenticatedClient("JobService");
        var fakeId = Guid.NewGuid();

        var response = await client.GetAsync($"/job/v1/jobs/{fakeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies that filtering jobs by status returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetJobs_WithStatusFilter_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("JobService");

        var response = await client.GetAsync("/job/v1/jobs?status=Pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that filtering jobs by technology returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetJobs_WithTechnologyFilter_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("JobService");

        var response = await client.GetAsync("/job/v1/jobs?technology=FDM");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
