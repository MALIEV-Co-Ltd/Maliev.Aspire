using System.Net;
using System.Net.Http.Json;
using Maliev.Aspire.Tests.Infrastructure;
using Maliev.Intranet.Shared;
using Maliev.Intranet.Shared.Dtos;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.People;

public class EmployeeLifecycleTests(ITestOutputHelper output) : MalievTestBase(output)
{
    [Fact]
    public async Task Employee_Onboarding_And_Preferences_Succeed()
    {
        // 1. Setup Clients
        var employeeClient = await CreateAuthenticatedClient("EmployeeService");
        var lifecycleClient = await CreateAuthenticatedClient("LifecycleService");

        // 2. Hire Employee
        Output.WriteLine("Scenario: Hire Employee");
        var hireRequest = new CreateEmployeeRequest
        {
            FirstName = "Somchai",
            LastName = "Maliev",
            Email = $"somchai.{Guid.NewGuid()}@maliev.com",
            Department = "Engineering",
            Title = "Software Engineer",
            StartDate = DateTime.UtcNow
        };

        var hireResponse = await employeeClient.PostAsJsonAsync("/employee/v1/employees", hireRequest);
        Assert.Equal(HttpStatusCode.Created, hireResponse.StatusCode);
        var employee = await hireResponse.Content.ReadFromJsonAsync<EmployeeSummaryDto>();
        Assert.NotNull(employee);
        Output.WriteLine($"✓ Employee hired: {employee.Name}");

        // 3. Verify Onboarding Triggered
        Output.WriteLine("Scenario: Verify Onboarding Checklist");
        // We might need a small delay for event-driven onboarding creation, 
        // but since we're in integration test, we can poll or expect synchronous if designed so.
        var checklist = await lifecycleClient.GetFromJsonAsync<OnboardingChecklistDto>($"/lifecycle/v1/onboarding/{employee.Id}/checklist");
        Assert.NotNull(checklist);
        Assert.NotEmpty(checklist.Tasks);
        Output.WriteLine($"✓ Onboarding checklist found with {checklist.Tasks.Count} tasks");

        // 4. Set User Preferences
        Output.WriteLine("Scenario: Set Dashboard Preferences");
        var prefRequest = new UpsertPreferenceRequest
        {
            PreferenceData = new Dictionary<string, object>
            {
                ["theme"] = "dark",
                ["sidebarExpanded"] = true,
                ["widgets"] = new[] { "orders", "revenue" }
            }
        };

        var prefResponse = await employeeClient.PutAsJsonAsync($"/employee/v1/preferences/dashboard", prefRequest);
        Assert.Equal(HttpStatusCode.OK, prefResponse.StatusCode);
        Output.WriteLine("✓ User preferences stored");
    }
}
