using Aspire.Hosting.Python;
using Maliev.Aspire.AppHost.OpenTelemetryCollector;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var config = Program.LoadSharedConfiguration(builder);
var infrastructure = Program.ConfigureInfrastructure(builder);
var databases = Program.ConfigureDatabases(infrastructure.Postgres);

// --- Monitoring (simple relative paths exactly like the sample) ---
var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v3.0.1")
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

        // Define these as formal Aspire Parameters to show up in the Dashboard
        var jwtPublicKey = builder.AddParameterFromConfig("JwtPublicKey", "Jwt:PublicKey", secret: true);
        var jwtIssuer = builder.AddParameterFromConfig("JwtIssuer", "Jwt:Issuer");
        var jwtAudience = builder.AddParameterFromConfig("JwtAudience", "Jwt:Audience");

        var corsAllowedOrigins = builder.AddParameter("CorsAllowedOrigins");
        // Convert the JSON array to a comma-separated string for easier environment injection
        var origins = builder.Configuration.GetSection("CORS:AllowedOrigins").Get<string[]>();
        builder.Configuration["Parameters:CorsAllowedOrigins"] = origins != null ? string.Join(",", origins) : string.Empty;

        return new SharedConfiguration(jwtPublicKey, jwtIssuer, jwtAudience, corsAllowedOrigins);
    }

    /// <summary>
    /// Configures infrastructure components (RabbitMQ, Redis, PostgreSQL).
    /// </summary>
    public static Infrastructure ConfigureInfrastructure(IDistributedApplicationBuilder builder)
    {
        // --- Messaging and Caching ---
        var erlangCookie = builder.AddParameterFromConfig("ErlangCookie", "RabbitMQ:ErlangCookie", secret: true);

        var rabbitmq = builder.AddRabbitMQ("rabbitmq")
                                .WithImageTag("4.2-alpine")
                                .WithEnvironment("RABBITMQ_ERLANG_COOKIE", erlangCookie);

        var redis = builder.AddRedis("redis")
                            .WithImageTag("8.4-alpine")
                            .WithRedisInsight(insight =>
                            {
                                insight.WithBindMount("redisinsight-data", "/data");
                            });

        // --- PostgreSQL Database Server ---
        var postgres = builder.AddPostgres("postgres-server")
                              .WithImageTag("18.1-alpine")
                              .WithArgs("-c", "max_connections=500") // Increase for many microservices
                              .WithPgAdmin(option => option.WithImageTag("8.14"));

        // --- Storage ---
        var storage = builder.AddContainer("storage", "minio/minio", "latest")
                             .WithArgs("server", "/data")
                             .WithHttpEndpoint(targetPort: 9000, name: "api")
                             .WithHttpEndpoint(targetPort: 9001, name: "console")
                             .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
                             .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin");

        return new Infrastructure(rabbitmq, redis, postgres, storage);
    }

    private static IResourceBuilder<ParameterResource> AddParameterFromConfig(
        this IDistributedApplicationBuilder builder, 
        string parameterName, 
        string configKey, 
        bool secret = false)
    {
        var parameter = builder.AddParameter(parameterName, secret: secret);
        builder.Configuration[$"Parameters:{parameterName}"] = builder.Configuration[configKey];
        return parameter;
    }

    /// <summary>
    /// Configures all service databases using the -app-db naming pattern.
    /// </summary>
    public static ServiceDatabases ConfigureDatabases(IResourceBuilder<PostgresServerResource> postgres)
    {
        return new ServiceDatabases(
            Accounting: postgres.AddDatabase("accounting-app-db"),
            Auth: postgres.AddDatabase("auth-app-db"),
            Career: postgres.AddDatabase("career-app-db"),
            Contact: postgres.AddDatabase("contact-app-db"),
            Country: postgres.AddDatabase("country-app-db"),
            Currency: postgres.AddDatabase("currency-app-db"),
            Customer: postgres.AddDatabase("customer-app-db"),
            Employee: postgres.AddDatabase("employee-app-db"),
            IAM: postgres.AddDatabase("iam-app-db"),
            Invoice: postgres.AddDatabase("invoice-app-db"),
            Material: postgres.AddDatabase("material-app-db"),
            Notification: postgres.AddDatabase("notification-app-db"),
            Order: postgres.AddDatabase("order-app-db"),
            Payment: postgres.AddDatabase("payment-app-db"),
            Pdf: postgres.AddDatabase("pdf-app-db"),
            PurchaseOrder: postgres.AddDatabase("purchaseorder-app-db"),
            Quotation: postgres.AddDatabase("quotation-app-db"),
            Receipt: postgres.AddDatabase("receipt-app-db"),
            Supplier: postgres.AddDatabase("supplier-app-db"),
            Upload: postgres.AddDatabase("upload-app-db")
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
        var iamService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_IAMService_Api>("maliev-iamservice-api")
                .WithReference(databases.IAM, "IamDbContext")
                .WaitFor(databases.IAM)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithHttpHealthCheck("/iam/readiness"),
            config);

        // Note: CountryService must be declared before CustomerService to be referenced
        var countryService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CountryService_Api>("maliev-countryservice-api")
                .WithReference(databases.Country, "CountryDbContext")
                .WaitFor(databases.Country)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/country/readiness"),
            config);

        var customerService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CustomerService_Api>("maliev-customerservice-api")
                .WithReference(databases.Customer, "CustomerDbContext")
                .WaitFor(databases.Customer)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(countryService)  // Enable service discovery
                .WithReference(iamService)
                .WithHttpHealthCheck("/customer/readiness")
                .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http")),  // Reference grafana to trigger monitoring stack
            config);

        var employeeService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_EmployeeService_Api>("maliev-employeeservice-api")
                .WithReference(databases.Employee, "EmployeeDbContext")
                .WaitFor(databases.Employee)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/employee/readiness"),
            config);

        var authService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_AuthService_Api>("maliev-authservice-api")
                .WithReference(databases.Auth, "AuthDbContext")
                .WaitFor(databases.Auth)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(customerService)
                .WithReference(employeeService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/auth/readiness"),
            config);

        // --- Business Services ---
        var accountingService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_AccountingService_Api>("maliev-accountingservice-api")
                .WithReference(databases.Accounting, "AccountingDbContext")
                .WaitFor(databases.Accounting)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/accounting/readiness"),
            config);

        var notificationService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_NotificationService_Api>("maliev-notificationservice-api")
                .WithReference(databases.Notification, "NotificationDbContext")
                .WaitFor(databases.Notification)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/notification/readiness"),
            config);

        var uploadService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_UploadService_Api>("maliev-uploadservice-api")
                .WithReference(databases.Upload, "UploadDbContext")
                .WaitFor(databases.Upload)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/upload/readiness"),
            config);

        var careerService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CareerService_Api>("maliev-careerservice-api")
                .WithReference(databases.Career, "CareerDbContext")
                .WaitFor(databases.Career)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(employeeService)
                .WithReference(uploadService)
                .WithReference(countryService)
                .WithReference(notificationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/career/readiness"),
            config);

        var contactService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ContactService_Api>("maliev-contactservice-api")
                .WithReference(databases.Contact, "ContactDbContext")
                .WaitFor(databases.Contact)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(uploadService)
                .WithReference(countryService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/contact/readiness"),
            config);

        var currencyService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CurrencyService_Api>("maliev-currencyservice-api")
                .WithReference(databases.Currency, "CurrencyDbContext")
                .WaitFor(databases.Currency)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/currency/readiness"),
            config);

        var quotationService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_QuotationService_Api>("maliev-quotationservice-api")
                .WithReference(databases.Quotation, "QuotationDbContext")
                .WaitFor(databases.Quotation)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/quotation/readiness"),
            config);

        var invoiceService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_InvoiceService_Api>("maliev-invoiceservice-api")
                .WithReference(databases.Invoice, "InvoiceDbContext")
                .WaitFor(databases.Invoice)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(currencyService)
                .WithReference(quotationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/invoice/readiness"),
            config);

        var materialService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_MaterialService_Api>("maliev-materialservice-api")
                .WithReference(databases.Material, "MaterialDbContext")
                .WaitFor(databases.Material)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/material/readiness"),
            config);

        var orderService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_OrderService_Api>("maliev-orderservice-api")
                .WithReference(databases.Order, "OrderDbContext")
                .WaitFor(databases.Order)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(customerService)
                .WithReference(materialService)
                .WithReference(uploadService)
                .WithReference(authService)
                .WithReference(employeeService)
                .WithReference(notificationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/order/readiness"),
            config);

        var paymentService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PaymentService_Api>("maliev-paymentservice-api")
                .WithReference(databases.Payment, "PaymentDbContext")
                .WaitFor(databases.Payment)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/payment/readiness"),
            config);

        var pdfService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PdfService_Api>("maliev-pdfservice-api")
                .WithReference(databases.Pdf, "PdfDbContext")
                .WaitFor(databases.Pdf)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(uploadService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/pdf/readiness"),
            config);

        var purchaseOrderService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PurchaseOrderService_Api>("maliev-purchaseorderservice-api")
                .WithReference(databases.PurchaseOrder, "PurchaseOrderDbContext")
                .WaitFor(databases.PurchaseOrder)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/purchase-order/readiness"),
            config);

        var receiptService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ReceiptService_Api>("maliev-receiptservice-api")
                .WithReference(databases.Receipt, "ReceiptDbContext")
                .WaitFor(databases.Receipt)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(invoiceService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/receipt/readiness"),
            config);

        var supplierService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_SupplierService_Api>("maliev-supplierservice-api")
                .WithReference(databases.Supplier, "SupplierDbContext")
                .WaitFor(databases.Supplier)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(purchaseOrderService)
                .WithReference(invoiceService)
                .WithReference(materialService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/supplier/readiness"),
            config);

        // --- Python Services ---
        var geometryService = builder.AddPythonApp("geometry-service", "../../Maliev.GeometryService", "src/main.py")
            .WithReference(infrastructure.RabbitMQ)
            .WithReference(infrastructure.Storage)
            .WithEnvironment("RABBITMQ_URI", infrastructure.RabbitMQ.GetConnectionString())
            .WithEnvironment("STORAGE_ENDPOINT", infrastructure.Storage.GetEndpoint("api"))
            .WithEnvironment("STORAGE_ACCESS_KEY", "minioadmin")
            .WithEnvironment("STORAGE_SECRET_KEY", "minioadmin")
            .WithHttpHealthCheck("/geometry/readiness")
            .WithHttpEndpoint(targetPort: 8080);
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
            .WithEnvironment("Jwt__SecurityKey", "test-key-at-least-32-characters-long-for-integration-tests") // Symmetric fallback
            .WithEnvironment("Jwt__Issuer", config.JwtIssuer)
            .WithEnvironment("Jwt__Audience", config.JwtAudience)
            .WithEnvironment("CORS__AllowedOrigins", config.CorsAllowedOrigins);
    }
}

/// <summary>
/// Shared configuration values loaded from configuration sources.
/// </summary>
record SharedConfiguration(
    IResourceBuilder<ParameterResource> JwtPublicKey,
    IResourceBuilder<ParameterResource> JwtIssuer,
    IResourceBuilder<ParameterResource> JwtAudience,
    IResourceBuilder<ParameterResource> CorsAllowedOrigins);

/// <summary>
/// Infrastructure resource references (messaging, caching, database server).
/// </summary>
record Infrastructure(
    IResourceBuilder<RabbitMQServerResource> RabbitMQ,
    IResourceBuilder<RedisResource> Redis,
    IResourceBuilder<PostgresServerResource> Postgres,
    IResourceBuilder<ContainerResource> Storage);

/// <summary>
/// Database references for all microservices.
/// </summary>
record ServiceDatabases(
    IResourceBuilder<PostgresDatabaseResource> Accounting,
    IResourceBuilder<PostgresDatabaseResource> Auth,
    IResourceBuilder<PostgresDatabaseResource> Career,
    IResourceBuilder<PostgresDatabaseResource> Contact,
    IResourceBuilder<PostgresDatabaseResource> Country,
    IResourceBuilder<PostgresDatabaseResource> Currency,
    IResourceBuilder<PostgresDatabaseResource> Customer,
    IResourceBuilder<PostgresDatabaseResource> Employee,
    IResourceBuilder<PostgresDatabaseResource> IAM,
    IResourceBuilder<PostgresDatabaseResource> Invoice,
    IResourceBuilder<PostgresDatabaseResource> Material,
    IResourceBuilder<PostgresDatabaseResource> Notification,
    IResourceBuilder<PostgresDatabaseResource> Order,
    IResourceBuilder<PostgresDatabaseResource> Payment,
    IResourceBuilder<PostgresDatabaseResource> Pdf,
    IResourceBuilder<PostgresDatabaseResource> PurchaseOrder,
    IResourceBuilder<PostgresDatabaseResource> Quotation,
    IResourceBuilder<PostgresDatabaseResource> Receipt,
    IResourceBuilder<PostgresDatabaseResource> Supplier,
    IResourceBuilder<PostgresDatabaseResource> Upload);
