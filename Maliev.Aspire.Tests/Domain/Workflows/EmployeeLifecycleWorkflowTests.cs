using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Workflows;

/// <summary>
/// Integration tests for the employee lifecycle workflow spanning employee, auth, IAM, leave, career, and compensation services.
/// </summary>
[Collection("AspireDomainTests")]
public class EmployeeLifecycleWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests the full employee lifecycle from hiring through auth login, IAM provisioning, leave balance, and career record.
    /// </summary>
    [Fact]
    public async Task FullEmployeeLifecycle_HireToLoginToIamToLeaveToCareer()
    {
        var employeeClient = _fixture.CreateAuthenticatedClient("EmployeeService");
        var authClient = _fixture.CreateClient("AuthService");
        var iamClient = _fixture.CreateAuthenticatedClient("IAMService");
        var leaveClient = _fixture.CreateAuthenticatedClient("LeaveService");
        var careerClient = _fixture.CreateAuthenticatedClient("CareerService");

        var testId = Guid.NewGuid().ToString("N")[..8];
        var testEmail = $"lifecycle.{testId}@maliev.com";

        var hireResponse = await employeeClient.PostAsJsonAsync("/employee/v1/employees", new
        {
            FirstName = "Lifecycle",
            LastName = $"Test {testId}",
            Email = testEmail,
            Department = "Engineering",
            Title = "Test Engineer",
            StartDate = DateTime.UtcNow
        });
        Assert.Equal(HttpStatusCode.Created, hireResponse.StatusCode);
        var employee = await hireResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = employee.GetProperty("id").GetGuid();
        _output.WriteLine($"[1] Employee hired: {employeeId}");

        var exchangeResponse = await authClient.PostAsJsonAsync("/auth/v1/exchange/google", new
        {
            Email = testEmail,
            FullName = $"Lifecycle Test {testId}"
        });
        var authContent = await exchangeResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"[2] Auth exchange: {exchangeResponse.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, exchangeResponse.StatusCode);
        var authResult = await exchangeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(authResult.TryGetProperty("access_token", out _));

        var principalsResponse = await TestHelpers.WaitForAsync(
            async () =>
            {
                var r = await iamClient.GetAsync($"/iam/v1/principals?search={testEmail}");
                return (Response: r, Content: await r.Content.ReadAsStringAsync());
            },
            until: result =>
            {
                if (!result.Response.IsSuccessStatusCode) return false;
                using var doc = JsonDocument.Parse(result.Content);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0) return true;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
                    return data.GetArrayLength() > 0;
                return false;
            },
            timeout: TimeSpan.FromSeconds(15),
            interval: TimeSpan.FromSeconds(2),
            message: $"IAM principal for {testEmail} not found within timeout");
        _output.WriteLine($"[3] IAM principal found");

        var leaveResponse = await leaveClient.GetAsync($"/leave/v1/leave-balances?employeeId={employeeId}");
        _output.WriteLine($"[4] Leave balance response: {leaveResponse.StatusCode}");
        Assert.True(leaveResponse.IsSuccessStatusCode,
            $"Leave balance check failed: {await leaveResponse.Content.ReadAsStringAsync()}");

        var careerResponse = await careerClient.GetAsync($"/career/v1/careers?employeeId={employeeId}");
        _output.WriteLine($"[5] Career record response: {careerResponse.StatusCode}");
        Assert.True(careerResponse.IsSuccessStatusCode,
            $"Career record check failed: {await careerResponse.Content.ReadAsStringAsync()}");

        Assert.NotEqual(Guid.Empty, employeeId);
    }

    /// <summary>
    /// Verifies that a compensation record can be created for a newly hired employee.
    /// </summary>
    [Fact]
    public async Task EmployeeCreation_CompensationRecord_CanBeCreated()
    {
        var employeeClient = _fixture.CreateAuthenticatedClient("EmployeeService");
        var compensationClient = _fixture.CreateAuthenticatedClient("CompensationService");

        var testId = Guid.NewGuid().ToString("N")[..8];

        var hireResponse = await employeeClient.PostAsJsonAsync("/employee/v1/employees", new
        {
            FirstName = "Comp",
            LastName = $"Test {testId}",
            Email = $"comp.{testId}@maliev.com",
            Department = "Production",
            Title = "3D Print Operator",
            StartDate = DateTime.UtcNow
        });
        Assert.Equal(HttpStatusCode.Created, hireResponse.StatusCode);
        var employee = await hireResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = employee.GetProperty("id").GetGuid();
        _output.WriteLine($"[1] Employee hired: {employeeId}");

        var compensationResponse = await compensationClient.PostAsJsonAsync("/compensation/v1/compensations", new
        {
            EmployeeId = employeeId,
            BaseSalary = 25000.00m,
            Currency = "THB",
            EffectiveDate = DateTime.UtcNow,
            PayFrequency = "Monthly"
        });
        var compContent = await compensationResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"[2] Compensation response: {compensationResponse.StatusCode} - {compContent}");
        Assert.True(
            compensationResponse.StatusCode == HttpStatusCode.Created ||
            compensationResponse.StatusCode == HttpStatusCode.OK,
            $"Expected 200/201 but got {compensationResponse.StatusCode}: {compContent}");
    }
}
