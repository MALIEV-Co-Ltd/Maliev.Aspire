using Maliev.Aspire.AppHost.OpenTelemetryCollector;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var config = Program.LoadSharedConfiguration(builder);
var infrastructure = Program.ConfigureInfrastructure(builder);
var databases = Program.ConfigureDatabases(infrastructure.Postgres);

// --- Monitoring (simple relative paths exactly like the sample) ---
var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v3.2.1")
    .WithBindMount("../prometheus", "/etc/prometheus", isReadOnly: true)
    .WithArgs("--web.enable-otlp-receiver", "--config.file=/etc/prometheus/prometheus.yml")
    .WithHttpEndpoint(targetPort: 9090)
    .WithUrlForEndpoint("http", u => u.DisplayText = "Prometheus Dashboard");

var grafana = builder.AddContainer("grafana", "grafana/grafana")
    .WithBindMount("../grafana/config", "/etc/grafana", isReadOnly: true)
    .WithBindMount("../grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
    .WithEnvironment("PROMETHEUS_ENDPOINT", prometheus.GetEndpoint("http"))
    .WithHttpEndpoint(targetPort: 3000)
    .WithUrlForEndpoint("http", u => u.DisplayText = "Grafana Dashboard");

builder.AddOpenTelemetryCollector("otelcollector", "../otelcollector/config.yaml")
    .WithEnvironment("PROMETHEUS_ENDPOINT", $"{prometheus.GetEndpoint("http")}/api/v1/otlp");

Program.ConfigureServices(builder, infrastructure, databases, config, grafana);

builder.Build().Run();

/// <summary>
/// Main program class containing configuration methods for the Aspire AppHost.
/// </summary>
static partial class Program
{
    /// <summary>
    /// Loads shared configuration from sharedsecrets.json and user secrets.
    /// </summary>
    public static SharedConfiguration LoadSharedConfiguration(IDistributedApplicationBuilder builder)
    {
        // Load shared secrets from sharedsecrets.json and user secrets
        builder.Configuration.AddJsonFile("sharedsecrets.json", optional: true);

        // These will be injected into all services via environment variables
        var jwtPublicKey = builder.Configuration["Jwt:PublicKey"] ?? "";
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "";
        var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "";
        var corsAllowedOrigins = builder.Configuration["CORS:AllowedOrigins"] ?? "";

        return new SharedConfiguration(jwtPublicKey, jwtIssuer, jwtAudience, corsAllowedOrigins);
    }

    /// <summary>
    /// Configures infrastructure components (RabbitMQ, Redis, PostgreSQL).
    /// </summary>
    public static Infrastructure ConfigureInfrastructure(IDistributedApplicationBuilder builder)
    {
        // --- Messaging and Caching ---
        var erlangCookie = builder.Configuration["RabbitMQ:ErlangCookie"];
        if (string.IsNullOrEmpty(erlangCookie))
        {
            throw new InvalidOperationException(
                "RabbitMQ ErlangCookie is not configured. Please add 'RabbitMQ:ErlangCookie' to sharedsecrets.json or another configuration source.");
        }

        var rabbitmq = builder.AddRabbitMQ("rabbitmq")
                              .WithEnvironment("RABBITMQ_ERLANG_COOKIE", erlangCookie);

        var redis = builder.AddRedis("redis")
                            .WithImageTag("8.4")
                            .WithRedisInsight(insight =>
                            {
                                insight.WithBindMount("redisinsight-data", "/data");
                            });

        // --- PostgreSQL Database Server ---
        var postgres = builder.AddPostgres("postgres-server")
                              .WithImageTag("18.1")
                              .WithPgAdmin(option => option.WithImageTag("9.10"));

        return new Infrastructure(rabbitmq, redis, postgres);
    }

    /// <summary>
    /// Configures all service databases using the -app-db naming pattern.
    /// </summary>
    public static ServiceDatabases ConfigureDatabases(IResourceBuilder<PostgresServerResource> postgres)
    {
        return new ServiceDatabases(
            Auth: postgres.AddDatabase("auth-app-db"),
            Career: postgres.AddDatabase("career-app-db"),
            Contact: postgres.AddDatabase("contact-app-db"),
            Country: postgres.AddDatabase("country-app-db"),
            Currency: postgres.AddDatabase("currency-app-db"),
            Customer: postgres.AddDatabase("customer-app-db"),
            Employee: postgres.AddDatabase("employee-app-db"),
            Invoice: postgres.AddDatabase("invoice-app-db"),
            Material: postgres.AddDatabase("material-app-db"),
            Order: postgres.AddDatabase("order-app-db"),
            Payment: postgres.AddDatabase("payment-app-db"),
            PurchaseOrder: postgres.AddDatabase("purchaseorder-app-db"),
            Quotation: postgres.AddDatabase("quotation-app-db"),
            Supplier: postgres.AddDatabase("supplier-app-db")
        );
    }

    /// <summary>
    /// Configures all microservices with their dependencies.
    /// Services are organized by dependency order: Core services first, then Auth, then Business services.
    /// </summary>
    public static void ConfigureServices(
        IDistributedApplicationBuilder builder,
        Infrastructure infrastructure,
        ServiceDatabases databases,
        SharedConfiguration config,
        IResourceBuilder<ContainerResource> grafana)
    {
        // --- Core Services (dependencies for Auth) ---
        // Note: CountryService must be declared before CustomerService to be referenced
        var countryService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CountryService_Api>("maliev-countryservice-api")
                .WithReference(databases.Country, "CountryDbContext")
                .WaitFor(databases.Country)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        var customerService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CustomerService_Api>("maliev-customerservice-api")
                .WithReference(databases.Customer, "CustomerDbContext")
                .WaitFor(databases.Customer)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(countryService)  // Enable service discovery
                .WithEnvironment("ExternalServices__CountryService__BaseUrl", countryService.GetEndpoint("http"))  // Map to standard config path
                .WithEnvironment("ExternalServices__CountryService__TimeoutInSeconds", "10")
                .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http")),  // Reference grafana to trigger monitoring stack
            config);

        var employeeService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_EmployeeService_Api>("maliev-employeeservice-api")
                .WithReference(databases.Employee, "EmployeeDbContext")
                .WaitFor(databases.Employee)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        // --- Auth Service ---
        // Note: .WithReference(customerService) enables service discovery.
        // The AuthService can now find the CustomerService using the endpoint injected by Aspire.
        // The configuration key for the URL will be "services:maliev-customerservice-api:http:0"
        // Your service code will need to read this key from IConfiguration.
        var authService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_AuthService_Api>("maliev-authservice-api")
                .WithReference(databases.Auth, "AuthDbContext")
                .WaitFor(databases.Auth)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(customerService)  // Enables AuthService to call CustomerService
                .WithReference(employeeService), // Enables AuthService to call EmployeeService
            config);

        // --- Business Services ---
        var careerService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CareerService_Api>("maliev-careerservice-api")
                .WithReference(databases.Career, "CareerDbContext")
                .WaitFor(databases.Career)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        var contactService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ContactService_Api>("maliev-contactservice-api")
                .WithReference(databases.Contact, "ContactDbContext")
                .WaitFor(databases.Contact)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(countryService)  // Enable service discovery
                .WithEnvironment("ExternalServices__CountryService__BaseUrl", countryService.GetEndpoint("http"))  // Map to standard config path
                .WithEnvironment("ExternalServices__CountryService__TimeoutInSeconds", "10"),
            config);


        var currencyService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CurrencyService_Api>("maliev-currencyservice-api")
                .WithReference(databases.Currency, "CurrencyDbContext")
                .WaitFor(databases.Currency)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        var invoiceService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_InvoiceService_Api>("maliev-invoiceservice-api")
                .WithReference(databases.Invoice, "InvoiceDbContext")
                .WaitFor(databases.Invoice)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        var materialService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_MaterialService_Api>("maliev-materialservice-api")
                .WithReference(databases.Material, "MaterialDbContext")
                .WaitFor(databases.Material)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        var orderService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_OrderService_Api>("maliev-orderservice-api")
                .WithReference(databases.Order, "OrderDbContext")
                .WaitFor(databases.Order)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        var paymentService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PaymentService_Api>("maliev-paymentservice-api")
                .WithReference(databases.Payment, "PaymentDbContext")
                .WaitFor(databases.Payment)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        // var pdfService = WithSharedSecrets(builder.AddProject<Projects.Maliev_PdfService_Api>("maliev-pdfservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis), config);
        // var predictionService = WithSharedSecrets(builder.AddProject<Projects.Maliev_PredictionService_Api>("maliev-predictionservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis), config);

        var purchaseOrderService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PurchaseOrderService_Api>("maliev-purchaseorderservice-api")
                .WithReference(databases.PurchaseOrder, "PurchaseOrderDbContext")
                .WaitFor(databases.PurchaseOrder)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        // var quotationRequestService = WithSharedSecrets(builder.AddProject<Projects.Maliev_QuotationRequestService_Api>("maliev-quotationrequestservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis), config);

        var quotationService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_QuotationService_Api>("maliev-quotationservice-api")
                .WithReference(databases.Quotation, "QuotationDbContext")
                .WaitFor(databases.Quotation)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis),
            config);

        // var receiptService = WithSharedSecrets(builder.AddProject<Projects.Maliev_ReceiptService_Api>("maliev-receiptservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis), config);

        var supplierService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_SupplierService_Api>("maliev-supplierservice-api")
                .WithReference(databases.Supplier, "SupplierDbContext")
                .WaitFor(databases.Supplier)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(purchaseOrderService)
                .WithReference(invoiceService)
                .WithReference(materialService),
            config);

        // var uploadService = WithSharedSecrets(builder.AddProject<Projects.Maliev_UploadService_Api>("maliev-uploadservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis), config);
    }

    /// <summary>
    /// Helper method to inject shared secrets and environment variables into a service.
    /// </summary>
    private static IResourceBuilder<ProjectResource> WithSharedSecrets(
        IResourceBuilder<ProjectResource> project,
        SharedConfiguration config)
    {
        return project
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("Jwt__PublicKey", config.JwtPublicKey)
            .WithEnvironment("Jwt__Issuer", config.JwtIssuer)
            .WithEnvironment("Jwt__Audience", config.JwtAudience)
            .WithEnvironment("CORS__AllowedOrigins", config.CorsAllowedOrigins);
    }
}

/// <summary>
/// Shared configuration values loaded from configuration sources.
/// </summary>
record SharedConfiguration(
    string JwtPublicKey,
    string JwtIssuer,
    string JwtAudience,
    string CorsAllowedOrigins);

/// <summary>
/// Infrastructure resource references (messaging, caching, database server).
/// </summary>
record Infrastructure(
    IResourceBuilder<RabbitMQServerResource> RabbitMQ,
    IResourceBuilder<RedisResource> Redis,
    IResourceBuilder<PostgresServerResource> Postgres);

/// <summary>
/// Database references for all microservices.
/// </summary>
record ServiceDatabases(
    IResourceBuilder<PostgresDatabaseResource> Auth,
    IResourceBuilder<PostgresDatabaseResource> Career,
    IResourceBuilder<PostgresDatabaseResource> Contact,
    IResourceBuilder<PostgresDatabaseResource> Country,
    IResourceBuilder<PostgresDatabaseResource> Currency,
    IResourceBuilder<PostgresDatabaseResource> Customer,
    IResourceBuilder<PostgresDatabaseResource> Employee,
    IResourceBuilder<PostgresDatabaseResource> Invoice,
    IResourceBuilder<PostgresDatabaseResource> Material,
    IResourceBuilder<PostgresDatabaseResource> Order,
    IResourceBuilder<PostgresDatabaseResource> Payment,
    IResourceBuilder<PostgresDatabaseResource> PurchaseOrder,
    IResourceBuilder<PostgresDatabaseResource> Quotation,
    IResourceBuilder<PostgresDatabaseResource> Supplier);
