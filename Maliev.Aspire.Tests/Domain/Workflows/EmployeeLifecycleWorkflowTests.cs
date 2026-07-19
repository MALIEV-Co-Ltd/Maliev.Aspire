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
        await AspireTestData.EnsureAnnualLeavePolicyAsync(_fixture);

        var testId = Guid.NewGuid().ToString("N")[..8];
        var testEmail = $"lifecycle.{testId}@maliev.com";

        var employee = await AspireTestData.CreateEmployeeAsync(_fixture, "LIFECYCLE", testEmail);
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
                var r = await iamClient.GetAsync($"/iam/v1/principals/by-email/{Uri.EscapeDataString(testEmail)}");
                return (Response: r, Content: await r.Content.ReadAsStringAsync());
            },
            until: result =>
            {
                if (!result.Response.IsSuccessStatusCode) return false;
                using var doc = JsonDocument.Parse(result.Content);
                var root = doc.RootElement;
                return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("principalId", out _);
            },
            timeout: TimeSpan.FromSeconds(15),
            interval: TimeSpan.FromSeconds(2),
            message: $"IAM principal for {testEmail} not found within timeout");
        _output.WriteLine($"[3] IAM principal found");

        var leaveResponse = await leaveClient.PostAsJsonSnakeCaseAsync($"/leave/v1/LeaveRequests/{employeeId}", new
        {
            LeaveType = 1,
            StartDate = DateTimeOffset.UtcNow.AddDays(14),
            EndDate = DateTimeOffset.UtcNow.AddDays(14),
            HalfDayPeriod = 0,
            Reason = "Lifecycle smoke test"
        });
        var leaveContent = await leaveResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"[4] Leave request response: {leaveResponse.StatusCode} - {leaveContent}");
        Assert.True(leaveResponse.StatusCode == HttpStatusCode.Created, $"Expected Created but got {leaveResponse.StatusCode}: {leaveContent}");

        var careerResponse = await TestHelpers.WaitForAsync(
            async () =>
            {
                var r = await careerClient.GetAsync($"/career/v1/employees/{employeeId}/training-records");
                return (Response: r, Content: await r.Content.ReadAsStringAsync());
            },
            until: result => result.Response.IsSuccessStatusCode,
            timeout: TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromSeconds(2),
            message: $"Career training record surface for employee {employeeId} was not reachable within timeout");
        _output.WriteLine($"[5] Career record response: {careerResponse.Response.StatusCode}");

        Assert.NotEqual(Guid.Empty, employeeId);
    }

    /// <summary>
    /// Verifies that a compensation record can be created for a newly hired employee.
    /// </summary>
    [Fact]
    public async Task EmployeeCreation_CompensationRecord_CanBeCreated()
    {
        var compensationClient = _fixture.CreateAuthenticatedClient("CompensationService");

        var testId = Guid.NewGuid().ToString("N")[..8];

        var employee = await AspireTestData.CreateEmployeeAsync(_fixture, "PAY", $"comp.{testId}@maliev.com");
        var employeeId = employee.GetProperty("id").GetGuid();
        _output.WriteLine($"[1] Employee hired: {employeeId}");

        var compensationResponse = await compensationClient.PostAsJsonSnakeCaseAsync($"/compensation/v1/employees/{employeeId}/compensation", new
        {
            NewBaseSalary = 25000.00m,
            Currency = "THB",
            CompensationType = 0,
            EffectiveDate = DateTime.UtcNow,
            ChangeType = "Initial",
            ChangeReason = "Initial compensation setup"
        });
        var compContent = await compensationResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"[2] Compensation response: {compensationResponse.StatusCode} - {compContent}");
        Assert.True(
            compensationResponse.StatusCode == HttpStatusCode.Created ||
            compensationResponse.StatusCode == HttpStatusCode.OK,
            $"Expected 200/201 but got {compensationResponse.StatusCode}: {compContent}");
    }
}
