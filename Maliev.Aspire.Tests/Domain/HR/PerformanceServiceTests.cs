using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.HR;

/// <summary>
/// Integration tests for the performance service.
/// </summary>
public class PerformanceServiceTests(ITestOutputHelper output) : MalievTestBase(output)
{
    /// <summary>
    /// Tests that an admin can create a performance review.
    /// </summary>
    [Fact]
    public async Task CreateReview_AsAdmin_Succeeds()
    {
        var employeeClient = await CreateAuthenticatedClient("EmployeeService");
        var perfClient = await CreateAuthenticatedClient("PerformanceService");

        // 1. Get an employee
        var empResponse = await employeeClient.GetAsync("/employee/v1/employees");
        var empResult = await empResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employee = empResult.GetProperty("data")[0];
        var employeeId = employee.GetProperty("id").GetGuid();

        // 2. Create review
        var request = new
        {
            ReviewCycle = 2, // Quarterly
            ReviewPeriodStart = new DateTime(2026, 1, 1),
            ReviewPeriodEnd = new DateTime(2026, 3, 31),
            SelfAssessment = "Initial self-assessment from integration test."
        };

        var response = await perfClient.PostAsJsonAsync($"/performance/v1/employees/{employeeId}/reviews", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(employeeId, result.GetProperty("employeeId").GetGuid());
    }

    /// <summary>
    /// Tests that performance reviews can be retrieved.
    /// </summary>
    [Fact]
    public async Task GetReviews_ReturnsData()
    {
        var employeeClient = await CreateAuthenticatedClient("EmployeeService");
        var perfClient = await CreateAuthenticatedClient("PerformanceService");

        // 1. Get an employee
        var empResponse = await employeeClient.GetAsync("/employee/v1/employees");
        var empResult = await empResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employee = empResult.GetProperty("data")[0];
        var employeeId = employee.GetProperty("id").GetGuid();

        // 2. Get reviews
        var response = await perfClient.GetAsync($"/performance/v1/employees/{employeeId}/reviews");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(result);
        Output.WriteLine($"Found {result.Count} reviews for employee {employeeId}");
    }
}
