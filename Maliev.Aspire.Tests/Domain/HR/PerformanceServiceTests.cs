using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.HR;

/// <summary>
/// Integration tests for the performance service.
/// </summary>
[Collection("AspireDomainTests")]
public class PerformanceServiceTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests that an admin can create a performance review.
    /// </summary>
    [Fact]
    public async Task CreateReview_AsAdmin_Succeeds()
    {
        var perfClient = _fixture.CreateAuthenticatedClient("PerformanceService");

        // 1. Create an employee
        var employee = await AspireTestData.CreateEmployeeAsync(_fixture, "PERF");
        var employeeId = employee.GetProperty("id").GetGuid();

        // 2. Create review
        var request = new
        {
            ReviewCycle = 2, // Quarterly
            ReviewPeriodStart = new DateTime(2026, 1, 1),
            ReviewPeriodEnd = new DateTime(2026, 3, 31),
            SelfAssessment = "Initial self-assessment from integration test."
        };

        var response = await perfClient.PostAsJsonSnakeCaseAsync($"/performance/v1/employees/{employeeId}/reviews", request);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Create review response: {response.StatusCode} - {content}");
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected Created but got {response.StatusCode}: {content}");
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(employeeId, result.GetProperty("employee_id").GetGuid());
    }

    /// <summary>
    /// Tests that performance reviews can be retrieved.
    /// </summary>
    [Fact]
    public async Task GetReviews_ReturnsData()
    {
        var perfClient = _fixture.CreateAuthenticatedClient("PerformanceService");

        // 1. Create an employee
        var employee = await AspireTestData.CreateEmployeeAsync(_fixture, "PERF");
        var employeeId = employee.GetProperty("id").GetGuid();

        // 2. Get reviews
        var response = await perfClient.GetAsync($"/performance/v1/employees/{employeeId}/reviews");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(result);
        _output.WriteLine($"Found {result.Count} reviews for employee {employeeId}");
    }
}
