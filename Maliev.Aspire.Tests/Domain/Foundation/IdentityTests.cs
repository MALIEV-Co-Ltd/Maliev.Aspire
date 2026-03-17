using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Foundation;

/// <summary>
/// Integration tests for identity management.
/// </summary>
public class IdentityTests : MalievTestBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public IdentityTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Tests that login with a new email triggers automatic employee provisioning.
    /// </summary>
    [Fact]
    public async Task Login_WithNewEmail_TriggersAutoProvisioning()
    {
        // Arrange
        Output.WriteLine("=== Login Auto-Provisioning Integration Test Starting ===");

        var testEmail = $"new.employee.{Guid.NewGuid().ToString("N")[..8]}@maliev.com";
        var googleUserId = "google-" + Guid.NewGuid().ToString("N");

        var googleExchangeRequest = new
        {
            email = testEmail,
            full_name = "New Employee",
            google_user_id = googleUserId
        };

        // Act - Step 1: Login (Auth Service)
        var authApiClient = CreateClient("AuthService");
        Output.WriteLine($"[Step 1] Attempting Google exchange for {testEmail}...");

        var response = await authApiClient.PostAsJsonAsync("/auth/v1/exchange/google", googleExchangeRequest);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(content);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString();
        Assert.NotNull(accessToken);
        Output.WriteLine("✓ Login successful, received Access Token");

        // Act - Step 2: Verify Employee (Employee Service)
        // Note: Eventual consistency check needed as Auth publishes event -> Employee consumes
        Output.WriteLine("\n[Step 2] Verifying employee record in Employee Service...");
        var employeeApiClient = CreateClient("EmployeeService");
        employeeApiClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage? empResponse = null;
        for (int i = 0; i < 5; i++)
        {
            empResponse = await employeeApiClient.GetAsync($"/employee/v1/employees/by-email/{testEmail}");
            if (empResponse.IsSuccessStatusCode) break;

            Output.WriteLine($"! Employee not found yet (Attempt {i + 1}/5). Waiting 2s...");
            await Task.Delay(2000);
        }

        Assert.NotNull(empResponse);
        empResponse!.EnsureSuccessStatusCode();

        var employee = await empResponse.Content.ReadFromJsonAsync<EmployeeResponse>();
        Assert.NotNull(employee);
        Assert.Equal(testEmail, employee.Email);

        Output.WriteLine($"✓ Employee record verified: ID={employee.Id}");
    }

    private class EmployeeResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
