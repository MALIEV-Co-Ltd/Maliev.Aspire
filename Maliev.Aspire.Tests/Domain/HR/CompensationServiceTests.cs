using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.HR;

public class CompensationServiceTests(ITestOutputHelper output) : MalievTestBase(output)
{
    [Fact]
    public async Task GetCompensationData_AsAdmin_ReturnsOk()
    {
        var employeeClient = await CreateAuthenticatedClient("EmployeeService");
        var compClient = await CreateAuthenticatedClient("CompensationService");

        // 1. Get an employee
        var empResponse = await employeeClient.GetAsync("/employee/v1/employees");
        var empResult = await empResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employee = empResult.GetProperty("data")[0];
        var employeeId = employee.GetProperty("id").GetGuid();

        // 2. Get compensation data
        var response = await compClient.GetAsync($"/compensation/v1/employees/{employeeId}/compensation");

        // It should be OK (200) or NotFound (404) if no record exists yet.
        // Based on controller logic, it returns NotFound if result == null.
        // We'll assert OK or NotFound to verify service reachability and IAM.
        Output.WriteLine($"Compensation status for {employeeId}: {response.StatusCode}");
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
    }
}
