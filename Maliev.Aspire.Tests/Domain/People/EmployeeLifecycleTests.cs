using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maliev.Aspire.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.People;

/// <summary>
/// Integration tests for the employee lifecycle.
/// </summary>
[Collection("AspireDomainTests")]
public class EmployeeLifecycleTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests the employee onboarding workflow and preferences.
    /// </summary>
    [Fact]
    public async Task Employee_Onboarding_And_Preferences_Succeed()
    {
        // 1. Setup Clients
        var employeeClient = _fixture.CreateAuthenticatedClient("EmployeeService");
        var lifecycleClient = _fixture.CreateAuthenticatedClient("LifecycleService");

        // 2. Hire Employee
        _output.WriteLine("Scenario: Hire Employee");
        var templateId = await AspireTestData.CreateOnboardingTemplateAsync(_fixture, "people-lifecycle");
        var employee = await AspireTestData.CreateEmployeeAsync(_fixture, "SOMCHAI");
        var employeeId = employee.GetProperty("id").GetGuid();
        _output.WriteLine($"Employee hired: {employeeId}");

        // 3. Verify Onboarding Triggered
        _output.WriteLine("Scenario: Verify Onboarding Checklist");
        var startResponse = await lifecycleClient.PostAsJsonAsync($"/lifecycle/v1/employees/{employeeId}/onboarding/start", new
        {
            startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            templateId
        });
        var startContent = await startResponse.Content.ReadAsStringAsync();
        Assert.True(
            startResponse.StatusCode == HttpStatusCode.Created ||
            startResponse.StatusCode == HttpStatusCode.Conflict && startContent.Contains("already", StringComparison.OrdinalIgnoreCase),
            $"Expected onboarding start to return Created or existing-checklist Conflict but got {startResponse.StatusCode}: {startContent}");

        var onboardingResponse = await TestHelpers.WaitForAsync(
            async () =>
            {
                var response = await lifecycleClient.GetAsync($"/lifecycle/v1/employees/{employeeId}/onboarding/status");
                return (Response: response, Content: await response.Content.ReadAsStringAsync());
            },
            until: result => result.Response.IsSuccessStatusCode,
            timeout: TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromSeconds(2),
            message: $"Onboarding status for employee {employeeId} was not reachable within timeout");
        Assert.Equal(HttpStatusCode.OK, onboardingResponse.Response.StatusCode);
        using var onboardingDocument = JsonDocument.Parse(onboardingResponse.Content);
        Assert.True(onboardingDocument.RootElement.TryGetProperty("items", out var items));
        Assert.NotEmpty(items.EnumerateArray());
        _output.WriteLine($"Onboarding checklist found with {items.GetArrayLength()} tasks");

        // 4. Set User Preferences
        _output.WriteLine("Scenario: Set Dashboard Preferences");
        var prefRequest = new
        {
            preferenceData = JsonSerializer.Serialize(new
            {
                theme = "dark",
                sidebarExpanded = true,
                widgets = new[] { "orders", "revenue" }
            })
        };

        var prefResponse = await employeeClient.PutAsJsonAsync($"/employee/v1/preferences/dashboard", prefRequest);
        var prefContent = await prefResponse.Content.ReadAsStringAsync();
        Assert.True(prefResponse.StatusCode == HttpStatusCode.OK,
            $"Expected preferences update to return OK but got {prefResponse.StatusCode}: {prefContent}");
        _output.WriteLine("User preferences stored");
    }
}
