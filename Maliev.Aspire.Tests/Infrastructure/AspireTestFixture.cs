using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Aspire.Hosting.Testing;
using Maliev.Aspire.ServiceDefaults.Testing;
using Xunit;

namespace Maliev.Aspire.Tests.Infrastructure;

/// <summary>
/// Shared fixture for all domain workflow tests. Starts the Aspire infrastructure once
/// and shares it across all tests in the collection, dramatically improving test performance.
/// </summary>
public class AspireTestFixture : IAsyncLifetime
{
    private static readonly string AspireTestAdminPasswordValue = $"CodexE2E-{Guid.NewGuid():N}!aA1";

    private static readonly IReadOnlyDictionary<string, string> ServiceLivenessPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AccountingService"] = "/accounting/aspire-liveness",
            ["AuthService"] = "/auth/aspire-liveness",
            ["CareerService"] = "/career/aspire-liveness",
            ["ChatbotService"] = "/chatbot/aspire-liveness",
            ["CommerceService"] = "/commerce/aspire-liveness",
            ["CompensationService"] = "/compensation/aspire-liveness",
            ["ComplianceService"] = "/compliance/aspire-liveness",
            ["ContactService"] = "/contact/aspire-liveness",
            ["CountryService"] = "/country/aspire-liveness",
            ["CurrencyService"] = "/currency/aspire-liveness",
            ["CustomerService"] = "/customer/aspire-liveness",
            ["DeliveryService"] = "/delivery/aspire-liveness",
            ["EmployeeService"] = "/employee/aspire-liveness",
            ["FacilityService"] = "/facility/aspire-liveness",
            ["GeometryService"] = "/geometry/aspire-liveness",
            ["IAMService"] = "/iam/aspire-liveness",
            ["InventoryService"] = "/inventory/aspire-liveness",
            ["InvoiceService"] = "/invoice/aspire-liveness",
            ["IntranetBff"] = "/intranet/aspire-liveness",
            ["JobService"] = "/job/aspire-liveness",
            ["LeaveService"] = "/leave/aspire-liveness",
            ["LifecycleService"] = "/lifecycle/aspire-liveness",
            ["MaterialService"] = "/material/aspire-liveness",
            ["NotificationService"] = "/notification/aspire-liveness",
            ["OrderService"] = "/order/aspire-liveness",
            ["PaymentService"] = "/payment/aspire-liveness",
            ["PdfService"] = "/pdf/aspire-liveness",
            ["PerformanceService"] = "/performance/aspire-liveness",
            ["PredictionService"] = "/predictionservice/aspire-liveness",
            ["PricingService"] = "/pricing/aspire-liveness",
            ["ProjectService"] = "/project/aspire-liveness",
            ["PurchaseOrderService"] = "/purchase-order/aspire-liveness",
            ["QuoteEngineBff"] = "/quote/aspire-liveness",
            ["QuotationService"] = "/quotation/aspire-liveness",
            ["ReceiptService"] = "/receipt/aspire-liveness",
            ["RegistryService"] = "/registry/aspire-liveness",
            ["SearchService"] = "/search/aspire-liveness",
            ["SupplierService"] = "/supplier/aspire-liveness",
            ["UploadService"] = "/upload/aspire-liveness",
            ["WebBff"] = "/web/aspire-liveness"
        };

    private readonly ConcurrentDictionary<string, Uri> _httpsBaseAddresses = new();
    private readonly ConcurrentDictionary<string, object> _serviceLivenessLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _serviceLivenessConfirmed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The distributed application factory for creating test clients.
    /// </summary>
    public DistributedApplicationFactory? AppFactory { get; private set; }

    /// <summary>
    /// The admin JWT token for authenticated requests.
    /// </summary>
    public string? AdminToken { get; private set; }

    /// <summary>
    /// Gets the Aspire-local automation employee email used by browser E2E tests.
    /// </summary>
    public string AspireTestAdminEmail { get; } = "aspire-automation@maliev.com";

    /// <summary>
    /// Gets the generated Aspire-local automation employee password for this test process.
    /// </summary>
    public string AspireTestAdminPassword => AspireTestAdminPasswordValue;

    /// <summary>
    /// Gets the Aspire-local limited employee email used by permission-boundary browser E2E tests.
    /// </summary>
    public string AspireTestLimitedEmployeeEmail { get; } = "aspire-limited@maliev.com";

    /// <summary>
    /// Gets the generated Aspire-local limited employee password for this test process.
    /// </summary>
    public string AspireTestLimitedEmployeePassword => AspireTestAdminPasswordValue;

    /// <summary>
    /// How long the AppHost took to start, for diagnostics.
    /// </summary>
    public TimeSpan StartupDuration { get; private set; }

    /// <summary>
    /// Initializes the test fixture by starting the Aspire application.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization.</returns>
    public async Task InitializeAsync()
    {
        Console.WriteLine("[AspireTestFixture] Starting AppHost in Testing environment...");
        var sw = Stopwatch.StartNew();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("AspireTestAdmin__Enabled", "true");
        Environment.SetEnvironmentVariable("AspireTestAdmin__Password", AspireTestAdminPasswordValue);

        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;

        AppFactory = new DistributedApplicationFactory(
            appHostAssembly.EntryPoint!.DeclaringType!,
            ["--environment", "Testing"]);

        await AppFactory.StartAsync();

        sw.Stop();
        StartupDuration = sw.Elapsed;
        Console.WriteLine($"[AspireTestFixture] AppHost started in {sw.Elapsed.TotalSeconds:F1}s");
    }

    /// <summary>
    /// Disposes of the test fixture by stopping the Aspire application.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal.</returns>
    public async Task DisposeAsync()
    {
        if (AppFactory != null)
        {
            await AppFactory.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates an authenticated HTTP client for the specified project using the admin token.
    /// Automatically resolves the HTTPS base address to avoid auth header stripping during redirects.
    /// </summary>
    /// <param name="projectName">The name of the project to create a client for.</param>
    /// <returns>An authenticated HTTP client for making requests to the project.</returns>
    public HttpClient CreateAuthenticatedClient(string projectName)
    {
        var token = GetAdminToken();
        var baseAddress = ResolveHttpsBaseAddress(projectName);

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = delegate { return true; }
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(100)
        };

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        EnsureServiceLiveness(projectName, client);

        return client;
    }

    /// <summary>
    /// Creates an unauthenticated HTTP client for the specified project.
    /// Automatically resolves the HTTPS base address to avoid redirect issues.
    /// </summary>
    /// <param name="projectName">The name of the project to create a client for.</param>
    /// <returns>An HTTP client for making unauthenticated requests to the project.</returns>
    public HttpClient CreateClient(string projectName)
    {
        var baseAddress = ResolveHttpsBaseAddress(projectName);

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = delegate { return true; }
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(100)
        };

        EnsureServiceLiveness(projectName, client);

        return client;
    }

    private void EnsureServiceLiveness(string projectName, HttpClient client)
    {
        if (_serviceLivenessConfirmed.ContainsKey(projectName))
        {
            return;
        }

        if (!ServiceLivenessPaths.TryGetValue(projectName, out var livenessPath))
        {
            Console.WriteLine($"[AspireTestFixture] {projectName}: no liveness path registered; skipping startup wait");
            return;
        }

        var gate = _serviceLivenessLocks.GetOrAdd(projectName, _ => new object());
        lock (gate)
        {
            if (_serviceLivenessConfirmed.ContainsKey(projectName))
            {
                return;
            }

            WaitForServiceLiveness(projectName, client, livenessPath);
            _serviceLivenessConfirmed[projectName] = true;
        }
    }

    private static void WaitForServiceLiveness(string projectName, HttpClient client, string livenessPath)
    {
        var timeout = projectName switch
        {
            "GeometryService" => TimeSpan.FromMinutes(8),
            "PaymentService" => TimeSpan.FromMinutes(6),
            "QuoteEngineBff" => TimeSpan.FromMinutes(6),
            _ => TimeSpan.FromMinutes(3)
        };
        var deadline = DateTime.UtcNow.Add(timeout);
        var attempts = 0;
        HttpStatusCode? lastStatus = null;
        string? lastContent = null;
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            attempts++;

            try
            {
                using var probeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var response = client.GetAsync(livenessPath, probeTimeout.Token).GetAwaiter().GetResult();
                lastStatus = response.StatusCode;
                lastContent = response.Content.ReadAsStringAsync(probeTimeout.Token).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine(
                        $"[AspireTestFixture] {projectName}: liveness ready at {livenessPath} after {attempts} attempt(s)");
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        var detail = string.Join(
            "; ",
            new[]
            {
                $"base address {client.BaseAddress}",
                $"attempts {attempts}",
                lastStatus is not null
                    ? $"last status {(int)lastStatus.Value} {lastStatus}: {Truncate(lastContent, 1_000)}"
                    : "last status <none>",
                lastException is not null
                    ? $"last exception {lastException.GetType().Name}: {lastException.Message}"
                    : "last exception <none>"
            });

        throw new TimeoutException(
            $"{projectName} did not become live at {livenessPath} within {timeout.TotalSeconds:N0}s; {detail}");
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Resolves the HTTPS base address for a project by probing the HTTP endpoint.
    /// Services use <c>UseHttpsRedirection()</c> which causes a 308 redirect from HTTP to HTTPS.
    /// The default <see cref="HttpClientHandler"/> strips Authorization headers on redirects,
    /// so we probe once to discover the HTTPS URL and use it directly for all subsequent requests.
    /// Results are cached per project name.
    /// </summary>
    /// <param name="projectName">The Aspire resource name of the project.</param>
    /// <returns>The HTTPS base address URI for the project.</returns>
    private Uri ResolveHttpsBaseAddress(string projectName)
    {
        return _httpsBaseAddresses.GetOrAdd(projectName, ResolveHttpsBaseAddressCore);
    }

    /// <summary>
    /// Core logic to discover the HTTPS base address by probing the HTTP endpoint.
    /// </summary>
    /// <param name="projectName">The Aspire resource name of the project.</param>
    /// <returns>The resolved base address URI.</returns>
    private Uri ResolveHttpsBaseAddressCore(string projectName)
    {
        if (string.Equals(projectName, "GeometryService", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveFactoryClientBaseAddress(projectName);
        }

        try
        {
            var httpsEndpoint = AppFactory!.GetEndpoint(projectName, "https");
            Console.WriteLine($"[AspireTestFixture] {projectName}: using HTTPS endpoint {httpsEndpoint}");
            return httpsEndpoint;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AspireTestFixture] HTTPS endpoint lookup failed for {projectName}: {ex.Message}");
        }

        try
        {
            var httpEndpoint = AppFactory!.GetEndpoint(projectName, "http");
            Console.WriteLine($"[AspireTestFixture] {projectName}: using HTTP endpoint {httpEndpoint}");
            return httpEndpoint;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AspireTestFixture] HTTP endpoint lookup failed for {projectName}: {ex.Message}");
        }

        return ResolveFactoryClientBaseAddress(projectName);
    }

    private Uri ResolveFactoryClientBaseAddress(string projectName)
    {
        var baseClient = AppFactory!.CreateHttpClient(projectName);
        var httpBase = baseClient.BaseAddress!;

        if (httpBase.Scheme == "https")
        {
            baseClient.Dispose();
            return httpBase;
        }

        using var probeHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = delegate { return true; }
        };

        using var probeClient = new HttpClient(probeHandler)
        {
            BaseAddress = httpBase,
            Timeout = TimeSpan.FromSeconds(10)
        };

        try
        {
            var response = probeClient.GetAsync("/").GetAwaiter().GetResult();

            if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399)
            {
                var location = response.Headers.Location;
                if (location != null)
                {
                    var httpsBase = location.IsAbsoluteUri
                        ? new Uri($"{location.Scheme}://{location.Authority}/")
                        : new Uri($"https://{httpBase.Host}:{location.Port}/");

                    Console.WriteLine($"[AspireTestFixture] {projectName}: HTTP {httpBase} -> HTTPS {httpsBase}");
                    baseClient.Dispose();
                    return httpsBase;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AspireTestFixture] HTTPS probe failed for {projectName}: {ex.Message}");
        }

        Console.WriteLine($"[AspireTestFixture] {projectName}: No redirect detected, using HTTP {httpBase}");
        baseClient.Dispose();
        return httpBase;
    }

    /// <summary>
    /// Gets the admin JWT token for authenticated requests, creating one if it doesn't exist.
    /// </summary>
    /// <returns>The admin JWT token string.</returns>
    public string GetAdminToken()
    {
        if (!string.IsNullOrEmpty(AdminToken))
        {
            return AdminToken;
        }

        AdminToken = IAMTestHelpers.CreateTestJWT(
            principalId: "00000000-0000-0000-0000-000000000002",
            permissions: "*");

        return AdminToken;
    }
}

/// <summary>
/// Collection definition for all domain workflow tests that need the Aspire infrastructure.
/// All tests in this collection share the same AspireTestFixture instance.
/// </summary>
[CollectionDefinition("AspireDomainTests")]
public class AspireDomainTestCollection : ICollectionFixture<AspireTestFixture>
{
}
