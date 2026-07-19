using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Integration;

/// <summary>
/// Integration tests verifying all services are discoverable and respond to health checks.
/// Uses the shared AppHost fixture so the full stack starts once for the suite.
/// </summary>
[Collection("AspireDomainTests")]
public class ServiceDiscoveryTests
{
    private readonly AspireTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceDiscoveryTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared AppHost fixture.</param>
    /// <param name="output">The test output helper.</param>
    public ServiceDiscoveryTests(AspireTestFixture fixture, ITestOutputHelper output)
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

        var client = _fixture.CreateClient(resourceName);

        var response = await TestHelpers.WaitForAsync(
            async () => await TryGetAsync(client, healthPath),
            until: result =>
            {
                if (result.IsSuccessStatusCode)
                {
                    return true;
                }

                _output.WriteLine($"  {resourceName}: {result.StatusCode}, retrying...");
                return false;
            },
            timeout: TimeSpan.FromSeconds(resourceName == "GeometryService" ? 120 : 45),
            interval: TimeSpan.FromSeconds(2),
            message: $"{resourceName} liveness check did not become healthy within timeout");

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

        var client = _fixture.CreateClient("IntranetBff");

        var response = await client.GetAsync("/intranet/aspire-liveness");

        _output.WriteLine($"  IntranetBff: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies the customer QuoteEngine BFF starts and responds to its liveness endpoint.
    /// </summary>
    [Fact]
    public async Task QuoteEngineBff_StartsAndRespondsToLivenessCheck()
    {
        _output.WriteLine("Checking QuoteEngineBff liveness...");

        var client = _fixture.CreateClient("QuoteEngineBff");

        var response = await client.GetAsync("/quote/aspire-liveness");

        _output.WriteLine($"  QuoteEngineBff: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies the BFF system health endpoint reports all downstream services as reachable.
    /// </summary>
    [Fact]
    public async Task IntranetBff_SystemHealth_ReportsAllServicesReachable()
    {
        _output.WriteLine("Checking BFF system health...");
        await WaitForGeometryServiceLivenessAsync();

        var client = _fixture.CreateAuthenticatedClient("IntranetBff");

        var healthResult = await TestHelpers.WaitForAsync(
            async () =>
            {
                var r = await client.GetAsync("/api/v1/system-health");
                var c = await r.Content.ReadAsStringAsync();
                return (Response: r, Content: c);
            },
            until: result =>
            {
                if (!result.Response.IsSuccessStatusCode)
                {
                    _output.WriteLine($"  BFF /api/v1/system-health: {result.Response.StatusCode}, retrying...");
                    return false;
                }

                if (ContainsUnreachableService(result.Content, out var serviceName))
                {
                    _output.WriteLine($"  {serviceName}: Unreachable, retrying...");
                    return false;
                }

                return true;
            },
            timeout: TimeSpan.FromSeconds(120),
            interval: TimeSpan.FromSeconds(5),
            message: "BFF system health did not report all downstream services reachable within timeout");

        var response = healthResult.Response;
        var body = healthResult.Content;
        var authenticateHeaders = string.Join(
            ", ",
            response.Headers.WwwAuthenticate.Select(header => header.ToString()));

        _output.WriteLine($"  BFF /api/v1/system-health: {response.StatusCode}");
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"BFF /api/v1/system-health returned {response.StatusCode}. WWW-Authenticate: {authenticateHeaders}. Body: {body}");

        using var doc = JsonDocument.Parse(body);
        var services = doc.RootElement.GetProperty("services");

        foreach (var service in services.EnumerateArray())
        {
            var name = service.GetProperty("serviceName").GetString();
            var status = service.GetProperty("status").GetString();
            _output.WriteLine($"  {name}: {status}");
            Assert.True(status != "Unreachable",
                $"Service '{name}' was unreachable. {DescribeServiceHealth(service)}");
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

        var client = _fixture.CreateClient(resourceName);

        var response = await TestHelpers.WaitForAsync(
            async () =>
            {
                var r = await client.GetAsync(readinessPath);
                var c = await r.Content.ReadAsStringAsync();
                return (Response: r, Content: c);
            },
            until: result =>
            {
                if (result.Response.IsSuccessStatusCode)
                    return true;
                if (!IsTransientReadinessFailure(result.Content))
                    return true; // Stop polling, let assertion below handle failure
                _output.WriteLine($"  {resourceName}: readiness pending, retrying...");
                return false;
            },
            timeout: TimeSpan.FromSeconds(100),
            interval: TimeSpan.FromSeconds(5),
            message: $"{resourceName} readiness check did not complete within timeout");

        _output.WriteLine($"  {resourceName}: {response.Response.StatusCode}");
        if (!response.Response.IsSuccessStatusCode)
            _output.WriteLine($"  Body: {response.Content}");

        Assert.True(response.Response.IsSuccessStatusCode,
            $"{resourceName} readiness failed with {response.Response.StatusCode}: {response.Content}");
    }

    private async Task WaitForGeometryServiceLivenessAsync()
    {
        _output.WriteLine("Waiting for GeometryService liveness before checking BFF aggregate health...");

        var client = _fixture.CreateClient("GeometryService");

        var response = await TestHelpers.WaitForAsync(
            async () => await TryGetAsync(client, "/geometry/liveness"),
            until: result =>
            {
                if (result.IsSuccessStatusCode)
                {
                    return true;
                }

                _output.WriteLine($"  GeometryService: {result.StatusCode}, retrying...");
                return false;
            },
            timeout: TimeSpan.FromSeconds(120),
            interval: TimeSpan.FromSeconds(2),
            message: "GeometryService liveness did not become healthy before BFF system health check");

        _output.WriteLine($"  GeometryService: {response.StatusCode}");
    }

    private static async Task<HttpResponseMessage> TryGetAsync(HttpClient client, string requestUri)
    {
        try
        {
            return await client.GetAsync(requestUri);
        }
        catch (HttpRequestException ex)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                ReasonPhrase = ex.Message
            };
        }
        catch (TaskCanceledException ex)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                ReasonPhrase = ex.Message
            };
        }
    }

    private static string DescribeServiceHealth(JsonElement service)
    {
        var parts = new List<string>();

        if (service.TryGetProperty("status", out var status))
        {
            parts.Add($"Status: {status.GetString()}.");
        }

        if (service.TryGetProperty("errorMessage", out var errorMessage) &&
            errorMessage.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(errorMessage.GetString()))
        {
            parts.Add($"Error: {errorMessage.GetString()}.");
        }

        if (service.TryGetProperty("lastChecked", out var lastChecked))
        {
            parts.Add($"Last checked: {lastChecked}.");
        }

        return string.Join(" ", parts);
    }

    private static bool IsTransientReadinessFailure(string content)
    {
        return content.Contains("IAM registration pending", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Not ready", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("start faulted", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("ReceiveTransport faulted", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("BrokerUnreachable", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("connection.start was never received", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsUnreachableService(string body, out string? serviceName)
    {
        serviceName = null;

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("services", out var services))
        {
            return false;
        }

        foreach (var service in services.EnumerateArray())
        {
            var status = service.GetProperty("status").GetString();
            if (!string.Equals(status, "Unreachable", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            serviceName = service.GetProperty("serviceName").GetString();
            return true;
        }

        return false;
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
        { "GeometryService", "/geometry/aspire-liveness" },
        { "QuoteEngineBff", "/quote/aspire-liveness" },
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
