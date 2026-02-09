using Aspire.Hosting.Testing;
using System.Net;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Integration;

/// <summary>
/// Shared fixture that starts the Aspire AppHost once for all service discovery tests.
/// </summary>
public class AspireAppHostFixture : IAsyncLifetime
{
    public DistributedApplicationFactory AppFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;
        AppFactory = new DistributedApplicationFactory(appHostAssembly.EntryPoint!.DeclaringType!);
        await AppFactory.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await AppFactory.DisposeAsync();
    }
}

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
    /// Verifies core services pass their full readiness check (DB, Redis, RabbitMQ, IAM).
    /// </summary>
    [Theory]
    [MemberData(nameof(CoreServices))]
    public async Task CoreService_PassesReadinessCheck(string resourceName, string readinessPath)
    {
        _output.WriteLine($"Checking readiness: {resourceName} at {readinessPath}");

        var client = _fixture.AppFactory.CreateHttpClient(resourceName);
        client.Timeout = TimeSpan.FromSeconds(30);

        var response = await client.GetAsync(readinessPath);
        var content = await response.Content.ReadAsStringAsync();

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
