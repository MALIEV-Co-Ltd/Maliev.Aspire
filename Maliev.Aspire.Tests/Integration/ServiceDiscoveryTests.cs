using Aspire.Hosting.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Integration;

/// <summary>
/// Shared fixture that starts the Aspire AppHost once for all service discovery tests.
/// </summary>
public class AspireAppHostFixture : IAsyncLifetime
{
    /// <summary>
    /// The distributed application factory for creating test clients.
    /// </summary>
    public DistributedApplicationFactory AppFactory { get; private set; } = null!;
    private string? _adminToken;

    /// <summary>
    /// Initializes the test fixture by starting the Aspire application.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization.</returns>
    public async Task InitializeAsync()
    {
        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;
        AppFactory = new DistributedApplicationFactory(appHostAssembly.EntryPoint!.DeclaringType!);
        await AppFactory.StartAsync();
    }

    /// <summary>
    /// Disposes of the test fixture by stopping the Aspire application.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal.</returns>
    public async Task DisposeAsync()
    {
        await AppFactory.DisposeAsync();
    }

    /// <summary>
    /// Gets the admin JWT token for authenticated requests.
    /// </summary>
    /// <returns>The admin JWT token string.</returns>
    public async Task<string> GetAdminTokenAsync()
    {
        if (!string.IsNullOrEmpty(_adminToken))
            return _adminToken;

        var authClient = AppFactory.CreateHttpClient("AuthService");
        var exchangeRequest = new
        {
            email = "admin@maliev.com",
            full_name = "Platform Owner",
            google_user_id = "google-platform-owner-id"
        };

        var response = await authClient.PostAsJsonAsync("/auth/v1/exchange/google", exchangeRequest);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        _adminToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token was null in auth response");

        var iamClient = AppFactory.CreateHttpClient("IAMService");
        iamClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        await iamClient.PostAsync("/iam/v1/principals/bootstrap/promote", null);

        // Re-acquire token to get updated permissions
        _adminToken = null;
        return await GetAdminTokenAsync();
    }
}

/// <summary>
/// Collection definition for service discovery tests using the Aspire AppHost fixture.
/// </summary>
[CollectionDefinition("AspireAppHost")]
public class AspireAppHostCollection : ICollectionFixture<AspireAppHostFixture>;

/// <summary>
/// Integration tests verifying all services are discoverable and respond to health checks.
/// Uses a shared AppHost fixture to avoid starting the full stack per test case.
/// </summary>
[Collection("AspireAppHost")]
public class ServiceDiscoveryTests
{
    private readonly AspireAppHostFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceDiscoveryTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared AppHost fixture.</param>
    /// <param name="output">The test output helper.</param>
    public ServiceDiscoveryTests(AspireAppHostFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Verifies every service responds to its liveness endpoint via Aspire service discovery.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllServices))]
    public async Task Service_RespondsToLivenessCheck(string resourceName, string healthPath)
    {
        _output.WriteLine($"Checking liveness: {resourceName} at {healthPath}");

        var client = _fixture.AppFactory.CreateHttpClient(resourceName);
        client.Timeout = TimeSpan.FromSeconds(15);

        var response = await client.GetAsync(healthPath);

        _output.WriteLine($"  {resourceName}: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies the BFF starts and responds to its health endpoint.
    /// </summary>
    [Fact]
    public async Task IntranetBff_StartsAndRespondsToHealthCheck()
    {
        _output.WriteLine("Checking BFF liveness...");

        var client = _fixture.AppFactory.CreateHttpClient("IntranetBff");
        client.Timeout = TimeSpan.FromSeconds(15);

        var response = await client.GetAsync("/intranet/aspire-liveness");

        _output.WriteLine($"  IntranetBff: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies the BFF system health endpoint reports all downstream services as reachable.
    /// </summary>
    [Fact]
    public async Task IntranetBff_SystemHealth_ReportsAllServicesReachable()
    {
        _output.WriteLine("Checking BFF system health...");

        var token = await _fixture.GetAdminTokenAsync();
        var client = _fixture.AppFactory.CreateHttpClient("IntranetBff");
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/system-health");

        _output.WriteLine($"  BFF /api/system-health: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var services = doc.RootElement.GetProperty("services");

        foreach (var service in services.EnumerateArray())
        {
            var name = service.GetProperty("serviceName").GetString();
            var status = service.GetProperty("status").GetString();
            _output.WriteLine($"  {name}: {status}");
            Assert.True(status != "Unreachable",
                $"Service '{name}' was unreachable — missing WithReference in AppHost?");
        }
    }

    /// <summary>
    /// Verifies core services pass their full readiness check (DB, Redis, RabbitMQ, IAM).
    /// Retries up to 20 times with 5s delay to allow IAM registration to complete after startup.
    /// </summary>
    [Theory]
    [MemberData(nameof(CoreServices))]
    public async Task CoreService_PassesReadinessCheck(string resourceName, string readinessPath)
    {
        _output.WriteLine($"Checking readiness: {resourceName} at {readinessPath}");

        var client = _fixture.AppFactory.CreateHttpClient(resourceName);
        client.Timeout = TimeSpan.FromSeconds(30);

        HttpResponseMessage response = null!;
        string content = string.Empty;
        const int maxRetries = 20;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            response = await client.GetAsync(readinessPath);
            content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                break;

            // Only retry for IAM registration pending — other failures are real errors
            if (!content.Contains("IAM registration pending"))
                break;

            _output.WriteLine($"  {resourceName}: IAM registration pending (attempt {attempt}/{maxRetries}), retrying in 5s...");
            await Task.Delay(5000);
        }

        _output.WriteLine($"  {resourceName}: {response.StatusCode}");
        if (!response.IsSuccessStatusCode)
            _output.WriteLine($"  Body: {content}");

        Assert.True(response.IsSuccessStatusCode,
            $"{resourceName} readiness failed with {response.StatusCode}: {content}");
    }

    /// <summary>
    /// All services registered in the Aspire AppHost with their liveness endpoints.
    /// Resource names match the AddProject() names in AppHost.cs.
    /// </summary>
    public static TheoryData<string, string> AllServices => new()
    {
        { "IAMService", "/iam/aspire-liveness" },
        { "AuthService", "/auth/aspire-liveness" },
        { "CustomerService", "/customer/aspire-liveness" },
        { "EmployeeService", "/employee/aspire-liveness" },
        { "CountryService", "/country/aspire-liveness" },
        { "RegistryService", "/registry/aspire-liveness" },
        { "UploadService", "/upload/aspire-liveness" },
        { "OrderService", "/order/aspire-liveness" },
        { "QuotationService", "/quotation/aspire-liveness" },
        { "InvoiceService", "/invoice/aspire-liveness" },
        { "MaterialService", "/material/aspire-liveness" },
        { "PaymentService", "/payment/aspire-liveness" },
        { "SupplierService", "/supplier/aspire-liveness" },
        { "AccountingService", "/accounting/aspire-liveness" },
        { "NotificationService", "/notification/aspire-liveness" },
        { "CareerService", "/career/aspire-liveness" },
        { "CompensationService", "/compensation/aspire-liveness" },
        { "ComplianceService", "/compliance/aspire-liveness" },
        { "ContactService", "/contact/aspire-liveness" },
        { "CurrencyService", "/currency/aspire-liveness" },
        { "ChatbotService", "/chatbot/aspire-liveness" },
        { "PricingService", "/pricing/aspire-liveness" },
        { "PerformanceService", "/performance/aspire-liveness" },
        { "LifecycleService", "/lifecycle/aspire-liveness" },
        { "LeaveService", "/leave/aspire-liveness" },
        { "PdfService", "/pdf/aspire-liveness" },
        { "PurchaseOrderService", "/purchase-order/aspire-liveness" },
        { "ReceiptService", "/receipt/aspire-liveness" },
        { "maliev-geometryservice", "/geometry/aspire-liveness" },
    };

    /// <summary>
    /// Core services that have database and infrastructure dependencies for readiness checks.
    /// </summary>
    public static TheoryData<string, string> CoreServices => new()
    {
        { "IAMService", "/iam/readiness" },
        { "AuthService", "/auth/readiness" },
        { "CustomerService", "/customer/readiness" },
        { "EmployeeService", "/employee/readiness" },
        { "CountryService", "/country/readiness" },
        { "OrderService", "/order/readiness" },
        { "PaymentService", "/payment/readiness" },
        { "InvoiceService", "/invoice/readiness" },
    };
}
