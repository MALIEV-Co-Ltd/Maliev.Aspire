using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Commercial;

/// <summary>
/// Integration tests for the ProjectService project management endpoints.
/// </summary>
[Collection("AspireDomainTests")]
public class ProjectServiceTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Verifies that creating a project with valid data returns 201 Created.
    /// </summary>
    [Fact]
    public async Task CreateProject_WithValidData_ReturnsCreated()
    {
        var projectClient = _fixture.CreateAuthenticatedClient("ProjectService");
        var customer = await AspireTestData.CreateCustomerAsync(_fixture, "project");
        var customerId = customer.GetProperty("id").GetGuid();
        var customerName = customer.GetProperty("name").GetString() ?? "Project Test";

        var createProjectRequest = new
        {
            Title = $"Test Project {Guid.NewGuid():N}"[..30],
            CustomerId = customerId,
            CustomerName = customerName,
            Description = "Integration test project",
            Currency = "THB"
        };

        var response = await projectClient.PostAsJsonAsync("/project/v1/projects", createProjectRequest);
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Create project response: {response.StatusCode} - {content}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(project.TryGetProperty("id", out _), $"Response should contain id: {content}");
    }

    /// <summary>
    /// Verifies that retrieving all projects returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetAllProjects_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("ProjectService");

        var response = await client.GetAsync("/project/v1/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that retrieving project statistics returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetProjectStats_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("ProjectService");

        var response = await client.GetAsync("/project/v1/projects/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that requesting a non-existent project by ID returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetProject_ByNonExistentId_Returns404()
    {
        var client = _fixture.CreateAuthenticatedClient("ProjectService");
        var fakeId = Guid.NewGuid();

        var response = await client.GetAsync($"/project/v1/projects/{fakeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies that adding a part to an existing project returns 200 OK or 201 Created.
    /// </summary>
    [Fact]
    public async Task CreateProject_AddPart_ReturnsOk()
    {
        var projectClient = _fixture.CreateAuthenticatedClient("ProjectService");
        var customer = await AspireTestData.CreateCustomerAsync(_fixture, "part");
        var customerId = customer.GetProperty("id").GetGuid();
        var customerName = customer.GetProperty("name").GetString() ?? "Part Test";

        var createProjectResponse = await projectClient.PostAsJsonAsync("/project/v1/projects", new
        {
            Title = $"Part Test {Guid.NewGuid():N}"[..25],
            CustomerId = customerId,
            CustomerName = customerName,
            Description = "Part test project",
            Currency = "THB"
        });
        Assert.Equal(HttpStatusCode.Created, createProjectResponse.StatusCode);
        var project = await createProjectResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var addPartResponse = await projectClient.PostAsJsonAsync($"/project/v1/projects/{projectId}/parts", new
        {
            FileName = "test-part.step",
            Description = "A test part"
        });

        _output.WriteLine($"Add part response: {addPartResponse.StatusCode}");
        Assert.True(
            addPartResponse.StatusCode == HttpStatusCode.Created ||
            addPartResponse.StatusCode == HttpStatusCode.OK,
            $"Expected 200/201 but got {addPartResponse.StatusCode}: {await addPartResponse.Content.ReadAsStringAsync()}");
    }
}
