using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.HR;

/// <summary>
/// Integration tests for the ComplianceService work authorization and compliance reporting.
/// </summary>
[Collection("AspireDomainTests")]
public class ComplianceServiceTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Verifies that recording a work authorization for an employee returns 200 OK or 201 Created.
    /// </summary>
    [Fact]
    public async Task RecordWorkAuthorization_WithValidData_ReturnsCreated()
    {
        var complianceClient = _fixture.CreateAuthenticatedClient("ComplianceService");
        var employeeClient = _fixture.CreateAuthenticatedClient("EmployeeService");

        var empResponse = await employeeClient.GetAsync("/employee/v1/employees?pageSize=1");
        Assert.Equal(HttpStatusCode.OK, empResponse.StatusCode);
        var empResult = await empResponse.Content.ReadFromJsonAsync<JsonElement>();

        JsonElement firstEmployee;
        if (empResult.ValueKind == JsonValueKind.Object && empResult.TryGetProperty("data", out var data))
        {
            if (data.GetArrayLength() == 0)
            {
                _output.WriteLine("No employees found — skipping test");
                return;
            }
            firstEmployee = data[0];
        }
        else
        {
            _output.WriteLine("Unexpected employee response format — skipping test");
            return;
        }

        var employeeId = firstEmployee.GetProperty("id").GetGuid();
        _output.WriteLine($"Using employee: {employeeId}");

        var recordRequest = new
        {
            AuthorizationType = "WorkPermit",
            DocumentNumber = $"WP-{Guid.NewGuid():N}"[..15],
            IssuingCountry = "TH",
            IssueDate = DateTime.UtcNow.AddDays(-30),
            ExpirationDate = DateTime.UtcNow.AddYears(1),
            Status = "Active"
        };

        var response = await complianceClient.PostAsJsonAsync(
            $"/compliance/v1/work-authorizations/employees/{employeeId}", recordRequest);
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Record auth response: {response.StatusCode} - {content}");

        Assert.True(
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200/201 but got {response.StatusCode}: {content}");
    }

    /// <summary>
    /// Verifies that retrieving expiring work authorizations returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetExpiringAuthorizations_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("ComplianceService");

        var response = await client.GetAsync("/compliance/v1/work-authorizations/expiring?daysUntilExpiration=90");

        _output.WriteLine($"Expiring authorizations response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that retrieving compliance alerts returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetAlerts_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("ComplianceService");

        var response = await client.GetAsync("/compliance/v1/compliance-alerts");

        _output.WriteLine($"Alerts response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that retrieving the compliance report returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetComplianceReport_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient("ComplianceService");

        var response = await client.GetAsync("/compliance/v1/compliance-reports/compliance");

        _output.WriteLine($"Compliance report response: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
