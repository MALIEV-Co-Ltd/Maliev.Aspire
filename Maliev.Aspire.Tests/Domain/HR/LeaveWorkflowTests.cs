using Maliev.Aspire.Tests.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.HR;

/// <summary>
/// Integration tests for the leave workflow.
/// </summary>
[Collection("AspireDomainTests")]
public class LeaveWorkflowTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests the full leave workflow from submission to approval.
    /// </summary>
    [Fact]
    public async Task FullLeaveWorkflow_SubmitAndApprove()
    {
        var leaveClient = _fixture.CreateAuthenticatedClient("LeaveService");
        await AspireTestData.EnsureAnnualLeavePolicyAsync(_fixture);

        // 1. Create an employee
        var employee = await AspireTestData.CreateEmployeeAsync(_fixture, "LEAVE");
        var employeeId = employee.GetProperty("id").GetGuid();
        var employeeName = employee.GetProperty("message").GetString();
        _output.WriteLine($"Working with employee: {employeeName} ({employeeId})");

        // 2. Submit Leave Request
        var submitRequest = new
        {
            LeaveType = 1, // Annual
            StartDate = DateTimeOffset.UtcNow.AddDays(7),
            EndDate = DateTimeOffset.UtcNow.AddDays(8),
            HalfDayPeriod = 0, // FullDay
            Reason = "Integration Test Leave"
        };

        var submitResponse = await leaveClient.PostAsJsonSnakeCaseAsync($"/leave/v1/LeaveRequests/{employeeId}", submitRequest);
        var submitContent = await submitResponse.Content.ReadAsStringAsync();
        Assert.True(submitResponse.StatusCode == HttpStatusCode.Created, $"Expected Created but got {submitResponse.StatusCode}: {submitContent}");

        var submittedResult = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = submittedResult.GetProperty("id").GetGuid();
        _output.WriteLine($"Leave request submitted: {requestId}");

        // 3. Verify it appears in requests list
        var listResponse = await leaveClient.GetAsync($"/leave/v1/LeaveRequests/employee/{employeeId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var requests = await listResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(requests);
        Assert.Contains(requests, r => r.GetProperty("id").GetGuid() == requestId);

        // 4. Approve the request
        var adminToken = _fixture.GetAdminToken();
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(adminToken);
        var adminId = Guid.Parse(jwtToken.Subject);

        var approveRequest = new
        {
            Decision = 2, // Approved
            Comments = "Approved by Integration Test"
        };

        var approveResponse = await leaveClient.PostAsJsonSnakeCaseAsync($"/leave/v1/LeaveRequests/{requestId}/decision?approverId={adminId}", approveRequest);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // 5. Verify status is Approved (2)
        var finalResponse = await leaveClient.GetAsync($"/leave/v1/LeaveRequests/employee/{employeeId}");
        var finalRequests = await finalResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(finalRequests);
        var updatedRequest = finalRequests.First(r => r.GetProperty("id").GetGuid() == requestId);
        Assert.Equal(2, updatedRequest.GetProperty("status").GetInt32());
    }
}
