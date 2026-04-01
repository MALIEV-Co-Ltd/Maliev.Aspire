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
        var customerClient = _fixture.CreateAuthenticatedClient("CustomerService");

        var custResponse = await customerClient.PostAsJsonAsync("/customer/v1/customers", new
        {
            FirstName = "Project",
            LastName = "Test",
            Email = $"project.test.{Guid.NewGuid():N}@example.com",
            Type = "Corporate",
            TaxId = "7777777777777"
        });
        Assert.Equal(HttpStatusCode.Created, custResponse.StatusCode);
        var customer = await custResponse.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = customer.GetProperty("id").GetGuid();

        var createProjectRequest = new
        {
            Name = $"Test Project {Guid.NewGuid():N}"[..30],
            CustomerId = customerId,
            Description = "Integration test project"
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
        var customerClient = _fixture.CreateAuthenticatedClient("CustomerService");

        var custResponse = await customerClient.PostAsJsonAsync("/customer/v1/customers", new
        {
            FirstName = "Part",
            LastName = "Test",
            Email = $"part.test.{Guid.NewGuid():N}@example.com",
            Type = "Retail"
        });
        var customer = await custResponse.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = customer.GetProperty("id").GetGuid();

        var createProjectResponse = await projectClient.PostAsJsonAsync("/project/v1/projects", new
        {
            Name = $"Part Test {Guid.NewGuid():N}"[..25],
            CustomerId = customerId,
            Description = "Part test project"
        });
        Assert.Equal(HttpStatusCode.Created, createProjectResponse.StatusCode);
        var project = await createProjectResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var addPartResponse = await projectClient.PostAsJsonAsync($"/project/v1/projects/{projectId}/parts", new
        {
            Name = "Test Part",
            Description = "A test part"
        });

        _output.WriteLine($"Add part response: {addPartResponse.StatusCode}");
        Assert.True(
            addPartResponse.StatusCode == HttpStatusCode.Created ||
            addPartResponse.StatusCode == HttpStatusCode.OK,
            $"Expected 200/201 but got {addPartResponse.StatusCode}: {await addPartResponse.Content.ReadAsStringAsync()}");
    }
}
