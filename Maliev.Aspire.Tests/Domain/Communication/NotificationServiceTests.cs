using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Communication;

public class NotificationServiceTests(ITestOutputHelper output) : MalievTestBase(output)
{
    [Fact]
    public async Task GetTemplates_AsAdmin_ReturnsOk()
    {
        var client = await CreateAuthenticatedClient("NotificationService");

        var response = await client.GetAsync("/notification/v1/templates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task CreateTemplate_AsAdmin_Succeeds()
    {
        var client = await CreateAuthenticatedClient("NotificationService");

        var request = new
        {
            TemplateKey = $"integration-test-{Guid.NewGuid():N}",
            Version = 1,
            Language = "en",
            ChannelType = 0, // Email
            ContentTemplate = "Hello {{name}}, this is a test notification.",
            Parameters = new[] { "name" }
        };

        var response = await client.PostAsJsonAsync("/notification/v1/templates", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(request.TemplateKey, result.GetProperty("templateKey").GetString());
    }
}
