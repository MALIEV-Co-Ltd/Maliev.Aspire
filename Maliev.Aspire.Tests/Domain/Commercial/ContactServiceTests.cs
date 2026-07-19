using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Commercial;

/// <summary>
/// Integration tests for the ContactService contact message management.
/// </summary>
[Collection("AspireDomainTests")]
public class ContactServiceTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Verifies that an anonymous user can submit a contact message and receive 201 Created.
    /// </summary>
    [Fact]
    public async Task CreateContactMessage_AsAnonymous_ReturnsCreated()
    {
        var client = _fixture.CreateClient("ContactService");
        var country = await AspireTestData.EnsureCountryAsync(_fixture);

        var request = new
        {
            FullName = $"Test User {Guid.NewGuid():N}"[..20],
            Email = $"contact.{Guid.NewGuid():N}@example.com",
            Subject = "Integration Test Inquiry",
            Message = "This is a test contact message from integration tests.",
            CountryId = country.GetProperty("id").GetGuid(),
            ContactType = 0
        };

        var response = await client.PostAsJsonAsync("/contact/v1/contacts", request);
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Create contact response: {response.StatusCode} - {content}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contact = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(contact.TryGetProperty("id", out _), $"Response should contain id: {content}");
    }

    /// <summary>
    /// Verifies that an authenticated admin can retrieve contact messages with 200 OK.
    /// </summary>
    [Fact]
    public async Task GetContactMessages_AsAdmin_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("ContactService");

        var response = await client.GetAsync("/contact/v1/contacts");

        _output.WriteLine($"Get contacts response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies the full contact workflow: create as anonymous, retrieve as admin, update status.
    /// </summary>
    [Fact]
    public async Task CreateAndRetrieveContact_FullWorkflow()
    {
        var anonymousClient = _fixture.CreateClient("ContactService");
        var adminClient = _fixture.CreateAuthenticatedClient("ContactService");
        var country = await AspireTestData.EnsureCountryAsync(_fixture);

        var request = new
        {
            FullName = "Workflow Test",
            Email = $"workflow.{Guid.NewGuid():N}@example.com",
            Subject = "Workflow Test Subject",
            Message = "Testing create → get → update status workflow.",
            CountryId = country.GetProperty("id").GetGuid(),
            ContactType = 0
        };

        var createResponse = await anonymousClient.PostAsJsonAsync("/contact/v1/contacts", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var contact = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var contactId = contact.GetProperty("id").GetInt32();
        _output.WriteLine($"Contact created: {contactId}");

        var getResponse = await adminClient.GetAsync($"/contact/v1/contacts/{contactId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(request.Subject, retrieved.GetProperty("subject").GetString());

        var updateResponse = await adminClient.PutAsJsonAsync($"/contact/v1/contacts/{contactId}/status", new
        {
            Status = 1
        });
        _output.WriteLine($"Update status response: {updateResponse.StatusCode}");
        Assert.True(updateResponse.IsSuccessStatusCode,
            $"Expected success but got {updateResponse.StatusCode}: {await updateResponse.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Verifies that requesting a non-existent contact by ID returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetContact_ByNonExistentId_Returns404()
    {
        var client = _fixture.CreateAuthenticatedClient("ContactService");
        const int fakeId = int.MaxValue;

        var response = await client.GetAsync($"/contact/v1/contacts/{fakeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
