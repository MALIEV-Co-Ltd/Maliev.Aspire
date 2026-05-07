using Maliev.Aspire.DatabaseSeeder.Seeding.Services.EmployeeService;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.IAMService;
using Maliev.Aspire.AppHost.Extensions;
using Maliev.Aspire.AppHost.OpenTelemetryCollector;
using Microsoft.Extensions.Configuration;

// Disable GSSAPI negotiation globally for the AppHost process and its probes.
// This silences the "SPNEGO cannot find mechanisms to negotiate" logs in postgres-server.
Environment.SetEnvironmentVariable("NPGSQL_GSSAPI_AUTHENTICATION", "false");
Environment.SetEnvironmentVariable("PGGSSENCMODE", "disable");

var builder = DistributedApplication.CreateBuilder(args);

var config = Program.LoadSharedConfiguration(builder);
var infrastructure = Program.ConfigureInfrastructure(builder);
var databases = Program.ConfigureDatabases(infrastructure.Postgres);

// --- Monitoring (Prometheus, Grafana, OpenTelemetry) ---
var prometheus = ConfigurePrometheus(builder);
var grafana = ConfigureGrafana(builder, prometheus);
var otelCollector = ConfigureOpenTelemetry(builder, prometheus);

Program.ConfigureServices(builder, infrastructure, databases, config, grafana, otelCollector);

builder.Build().Run();

// --- Local Infrastructure Configuration Functions ---

static IResourceBuilder<ContainerResource> ConfigurePrometheus(IDistributedApplicationBuilder builder)
{
    return builder.AddContainer("prometheus", "prom/prometheus", "v3.0.1")
        .WithBindMount("../prometheus", "/etc/prometheus", isReadOnly: true)
        .WithArgs("--web.enable-otlp-receiver", "--config.file=/etc/prometheus/prometheus.yml")
        .WithHttpEndpoint(targetPort: 9090)
        .WithUrlForEndpoint("http", u => u.DisplayText = "Prometheus Dashboard");
}

static IResourceBuilder<ContainerResource> ConfigureGrafana(
    IDistributedApplicationBuilder builder,
    IResourceBuilder<ContainerResource> prometheus)
{
    return builder.AddContainer("grafana", "grafana/grafana")
        .WithBindMount("../grafana/config", "/etc/grafana", isReadOnly: true)
        .WithBindMount("../grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
        .WithEnvironment("PROMETHEUS_ENDPOINT", prometheus.GetEndpoint("http"))
        .WithHttpEndpoint(targetPort: 3000)
        .WithUrlForEndpoint("http", u => u.DisplayText = "Grafana Dashboard");
}

static IResourceBuilder<ContainerResource> ConfigureOpenTelemetry(
    IDistributedApplicationBuilder builder,
    IResourceBuilder<ContainerResource> prometheus)
{
    return builder.AddOpenTelemetryCollector("otelcollector", "../otelcollector/config.yaml")
        .WithEnvironment("PROMETHEUS_ENDPOINT", $"{prometheus.GetEndpoint("http")}/api/v1/otlp");
}
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
        builder.Configuration.AddEnvironmentVariables();

        // Define these as formal Aspire Parameters to show up in the Dashboard
        var jwtSecurityKey = builder.AddParameterFromConfig("JwtSecurityKey", "Jwt:SecurityKey", secret: true);
        var jwtPrivateKey = builder.AddParameterFromConfig("JwtPrivateKey", "Jwt:PrivateKey", secret: true);
        var jwtPublicKey = builder.AddParameterFromConfig("JwtPublicKey", "Jwt:PublicKey", secret: true);
        var jwtIssuer = builder.AddParameterFromConfig("JwtIssuer", "Jwt:Issuer");
        var jwtAudience = builder.AddParameterFromConfig("JwtAudience", "Jwt:Audience");

        var googleClientId = builder.AddParameterFromConfig("GoogleClientId", "Authentication:Google:ClientId", secret: true);
        var googleClientSecret = builder.AddParameterFromConfig("GoogleClientSecret", "Authentication:Google:ClientSecret", secret: true);

        var aspireTestAdminEnabled = builder.AddParameter("AspireTestAdminEnabled");
        builder.Configuration["Parameters:AspireTestAdminEnabled"] =
            builder.Configuration["AspireTestAdmin:Enabled"] ?? "false";
        var aspireTestAdminPassword = builder.AddParameter("AspireTestAdminPassword", secret: true);
        builder.Configuration["Parameters:AspireTestAdminPassword"] =
            builder.Configuration["AspireTestAdmin:Password"] ?? string.Empty;

        var corsAllowedOrigins = builder.AddParameter("CorsAllowedOrigins");
        // Convert the JSON array to a comma-separated string for easier environment injection
        var origins = builder.Configuration.GetSection("CORS:AllowedOrigins").Get<string[]>();
        builder.Configuration["Parameters:CorsAllowedOrigins"] = origins != null ? string.Join(",", origins) : string.Empty;

        // GCP credentials for UploadService (loaded from sharedsecrets.json)
        var gcpProjectId = builder.AddParameterFromConfig("GcpProjectId", "GCP:ProjectId", secret: true);
        var gcpServiceAccountKeyBase64 = builder.AddParameterFromConfig("GcpServiceAccountKeyBase64", "GCP:ServiceAccountKeyBase64", secret: true);

        const string devNotificationEncryptionKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
        var notificationEncryptionKey = builder.AddParameter("NotificationEncryptionKey", secret: true);
        builder.Configuration["Parameters:NotificationEncryptionKey"] =
            builder.Configuration["Encryption:DataProtectionKey"] ?? devNotificationEncryptionKey;

        return new SharedConfiguration(
            JwtSecurityKey: jwtSecurityKey,
            JwtPrivateKey: jwtPrivateKey,
            JwtPublicKey: jwtPublicKey,
            JwtIssuer: jwtIssuer,
            JwtAudience: jwtAudience,
            GoogleClientId: googleClientId,
            GoogleClientSecret: googleClientSecret,
            AspireTestAdminEnabled: aspireTestAdminEnabled,
            AspireTestAdminPassword: aspireTestAdminPassword,
            CorsAllowedOrigins: corsAllowedOrigins,
            GcpProjectId: gcpProjectId,
            GcpServiceAccountKeyBase64: gcpServiceAccountKeyBase64,
            NotificationEncryptionKey: notificationEncryptionKey
            );
    }

    /// <summary>
    /// Configures infrastructure components (RabbitMQ, Redis, PostgreSQL).
    /// </summary>
    public static Infrastructure ConfigureInfrastructure(IDistributedApplicationBuilder builder)
    {
        var rabbitmq = ConfigureRabbitMQ(builder);
        var redis = ConfigureRedis(builder);
        var postgres = ConfigurePostgres(builder);

        return new Infrastructure(rabbitmq, redis, postgres);

        // --- Local Infrastructure Component Functions ---

        static IResourceBuilder<RabbitMQServerResource> ConfigureRabbitMQ(IDistributedApplicationBuilder builder)
        {
            var erlangCookie = builder.AddParameterFromConfig("ErlangCookie", "RabbitMQ:ErlangCookie", secret: true);
            var rabbitUser = builder.AddParameterFromConfig("RabbitMQUser", "RabbitMQ:Username");
            var rabbitPass = builder.AddParameterFromConfig("RabbitMQPass", "RabbitMQ:Password", secret: true);

            return builder.AddRabbitMQ("rabbitmq", userName: rabbitUser, password: rabbitPass)
                .WithImageTag("4.2-management-alpine")
                .WithEnvironment("RABBITMQ_ERLANG_COOKIE", erlangCookie)
                .WithHttpEndpoint(targetPort: 15672, name: "management")
                .WithUrlForEndpoint("management", u => u.DisplayText = "RabbitMQ Management");
        }

        static IResourceBuilder<RedisResource> ConfigureRedis(IDistributedApplicationBuilder builder)
        {
            var redis = builder.AddRedis("redis")
                .WithImageTag("8.4-alpine");

            redis.WithRedisInsight(insight =>
            {
                insight.WithBindMount("redisinsight-data", "/data")
                    .WithUrlForEndpoint("http", u => u.DisplayText = "RedisInsight Dashboard");
            });

            return redis;
        }

        static IResourceBuilder<PostgresServerResource> ConfigurePostgres(IDistributedApplicationBuilder builder)
        {
            return builder.AddPostgres("postgres-server")
                .WithImageTag("18-alpine")
                .WithArgs("-c", "max_connections=2000")
                .WithEnvironment("PGGSSENCMODE", "disable") // Disable GSSAPI for internal container probes (pg_isready)
                .WithPgAdmin(option =>
                {
                    option.WithImageTag("9.11")
                        .WithEnvironment("PGGSSENCMODE", "disable") // Disable GSSAPI for pgAdmin connections
                        .WithEnvironment("PYTHONWARNINGS", "ignore") // Suppress SyntaxWarnings from sshtunnel in Python 3.14+
                        .WithUrlForEndpoint("http", u => u.DisplayText = "pgAdmin Dashboard");
                });
        }
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
            Chatbot: postgres.AddDatabase("chatbot-app-db"),
            Compensation: postgres.AddDatabase("compensation-app-db"),
            Compliance: postgres.AddDatabase("compliance-app-db"),
            Contact: postgres.AddDatabase("contact-app-db"),
            Country: postgres.AddDatabase("country-app-db"),
            Currency: postgres.AddDatabase("currency-app-db"),
            Customer: postgres.AddDatabase("customer-app-db"),
            Delivery: postgres.AddDatabase("delivery-app-db"),
            Employee: postgres.AddDatabase("employee-app-db"),
            IAM: postgres.AddDatabase("iam-app-db"),
            Intranet: postgres.AddDatabase("intranet-app-db"),
            Invoice: postgres.AddDatabase("invoice-app-db"),
            Leave: postgres.AddDatabase("leave-app-db"),
            Lifecycle: postgres.AddDatabase("lifecycle-app-db"),
            Material: postgres.AddDatabase("material-app-db"),
            Notification: postgres.AddDatabase("notification-app-db"),
            Order: postgres.AddDatabase("order-app-db"),
            Payment: postgres.AddDatabase("payment-app-db"),
            Pdf: postgres.AddDatabase("pdf-app-db"),
            Performance: postgres.AddDatabase("performance-app-db"),
            Prediction: postgres.AddDatabase("prediction-app-db"),
            Pricing: postgres.AddDatabase("pricing-app-db"),
            PurchaseOrder: postgres.AddDatabase("purchaseorder-app-db"),
            Quotation: postgres.AddDatabase("quotation-app-db"),
            Receipt: postgres.AddDatabase("receipt-app-db"),
            Registry: postgres.AddDatabase("registry-app-db"),
            Search: postgres.AddDatabase("search-app-db"),
            Supplier: postgres.AddDatabase("supplier-app-db"),
            Upload: postgres.AddDatabase("upload-app-db"),
            Facility: postgres.AddDatabase("facility-app-db"),
            Inventory: postgres.AddDatabase("inventory-app-db"),
            Job: postgres.AddDatabase("job-app-db"),
            Project: postgres.AddDatabase("project-app-db")
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
        IResourceBuilder<ContainerResource> grafana,
        IResourceBuilder<ContainerResource> otelCollector)
    {
        var environmentName = builder.Environment.EnvironmentName;

        void ConfigureAspireTestAdminSeeder(IResourceBuilder<ProjectResource> seeder)
        {
            seeder
                .WithEnvironment("AspireTestAdmin__Enabled", config.AspireTestAdminEnabled)
                .WithEnvironment("AspireTestAdmin__Password", config.AspireTestAdminPassword);
        }

        // --- Core Services (dependencies for Auth) ---
        var iamService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_IAMService_Api>("IAMService")
                .WithReference(databases.IAM, "IamDbContext")
                .WaitFor(databases.IAM)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithHttpHealthCheck("/iam/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName)
            .SeedDatabase<IAMDatabaseSeeder>(databases.IAM, configureSeeder: ConfigureAspireTestAdminSeeder);

        // Note: CountryService must be declared before CustomerService to be referenced
        var countryService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CountryService_Api>("CountryService")
                .WithReference(databases.Country, "CountryDbContext")
                .WaitFor(databases.Country)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/country/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var registryService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_RegistryService_Api>("RegistryService")
                .WithReference(databases.Registry, "RegistryDbContext")
                .WaitFor(databases.Registry)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/registry/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var facilityService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_FacilityService_Api>("FacilityService")
                .WithReference(databases.Facility, "FacilityDbContext")
                .WaitFor(databases.Facility)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/facility/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var uploadService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_UploadService_Api>("UploadService")
                .WithReference(databases.Upload, "UploadDbContext")
                .WaitFor(databases.Upload)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/upload/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName)
            .WithEnvironment("GCP__ProjectId", config.GcpProjectId)
            .WithEnvironment("GCP__ServiceAccountKeyBase64", config.GcpServiceAccountKeyBase64);

        var customerService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CustomerService_Api>("CustomerService")
                .WithReference(databases.Customer, "CustomerDbContext")
                .WaitFor(databases.Customer)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(countryService)
                .WithReference(uploadService)
                .WaitFor(uploadService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/customer/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var employeeService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_EmployeeService_Api>("EmployeeService")
                .WithReference(databases.Employee, "EmployeeDbContext")
                .WaitFor(databases.Employee)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/employee/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName)
            .SeedDatabase<EmployeeDatabaseSeeder>(databases.Employee, configureSeeder: ConfigureAspireTestAdminSeeder);

        var authService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_AuthService_Api>("AuthService")
                .WithReference(databases.Auth, "AuthDbContext")
                .WaitFor(databases.Auth)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(customerService)
                .WithReference(employeeService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/auth/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        // --- Business Services ---
        var accountingService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_AccountingService_Api>("AccountingService")
                .WithReference(databases.Accounting, "AccountingDbContext")
                .WaitFor(databases.Accounting)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/accounting/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);



        var notificationService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_NotificationService_Api>("NotificationService")
                .WithReference(databases.Notification, "NotificationDbContext")
                .WaitFor(databases.Notification)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(customerService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/notification/aspire-liveness")
                .WithEnvironment("Encryption__DataProtectionKey", config.NotificationEncryptionKey),
            config,
            grafana,
            otelCollector,
            environmentName);

        var careerService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CareerService_Api>("CareerService")
                .WithReference(databases.Career, "CareerDbContext")
                .WaitFor(databases.Career)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(employeeService)
                .WithReference(uploadService)
                .WaitFor(uploadService)
                .WithReference(countryService)
                .WithReference(notificationService)
                .WaitFor(notificationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/career/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var compensationService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CompensationService_Api>("CompensationService")
                .WithReference(databases.Compensation, "CompensationDbContext")
                .WaitFor(databases.Compensation)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(employeeService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/compensation/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var complianceService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ComplianceService_Api>("ComplianceService")
                .WithReference(databases.Compliance, "ComplianceDbContext")
                .WaitFor(databases.Compliance)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(employeeService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/compliance/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var leaveService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_LeaveService_Api>("LeaveService")
                .WithReference(databases.Leave, "LeaveDbContext")
                .WaitFor(databases.Leave)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(employeeService)
                .WithReference(notificationService)
                .WaitFor(notificationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/leave/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var lifecycleService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_LifecycleService_Api>("LifecycleService")
                .WithReference(databases.Lifecycle, "LifecycleDbContext")
                .WaitFor(databases.Lifecycle)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(employeeService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/lifecycle/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var performanceService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PerformanceService_Api>("PerformanceService")
                .WithReference(databases.Performance, "PerformanceDbContext")
                .WaitFor(databases.Performance)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(employeeService)
                .WithReference(notificationService)
                .WaitFor(notificationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/performance/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var contactService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ContactService_Api>("ContactService")
                .WithReference(databases.Contact, "ContactDbContext")
                .WaitFor(databases.Contact)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(uploadService)
                .WaitFor(uploadService)
                .WithReference(countryService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/contact/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var currencyService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CurrencyService_Api>("CurrencyService")
                .WithReference(databases.Currency, "CurrencyDbContext")
                .WaitFor(databases.Currency)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/currency/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var quotationService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_QuotationService_Api>("QuotationService")
                .WithReference(databases.Quotation, "QuotationDbContext")
                .WaitFor(databases.Quotation)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithReference(customerService)
                .WithHttpHealthCheck("/quotation/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var invoiceService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_InvoiceService_Api>("InvoiceService")
                .WithReference(databases.Invoice, "InvoiceDbContext")
                .WaitFor(databases.Invoice)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(currencyService)
                .WithReference(quotationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/invoice/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var materialService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_MaterialService_Api>("MaterialService")
                .WithReference(databases.Material, "MaterialDbContext")
                .WaitFor(databases.Material)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/material/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var pricingService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PricingService_Api>("PricingService")
                .WithReference(databases.Pricing, "PricingDbContext")
                .WaitFor(databases.Pricing)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(materialService)
                .WithReference(currencyService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/pricing/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var orderService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_OrderService_Api>("OrderService")
                .WithReference(databases.Order, "OrderDbContext")
                .WaitFor(databases.Order)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(customerService)
                .WithReference(materialService)
                .WithReference(uploadService)
                .WaitFor(uploadService)
                .WithReference(authService)
                .WithReference(employeeService)
                .WithReference(notificationService)
                .WaitFor(notificationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/order/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var deliveryService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_DeliveryService_Api>("DeliveryService")
                .WithReference(databases.Delivery, "DeliveryDbContext")
                .WaitFor(databases.Delivery)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(orderService)
                .WithReference(customerService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/delivery/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var paymentService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PaymentService_Api>("PaymentService")
                .WithReference(databases.Payment, "PaymentDbContext")
                .WaitFor(databases.Payment)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/payment/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var pdfService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PdfService_Api>("PdfService")
                .WithReference(databases.Pdf, "PdfDbContext")
                .WaitFor(databases.Pdf)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(uploadService)
                .WaitFor(uploadService)
                .WithReference(deliveryService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/pdf/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var purchaseOrderService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PurchaseOrderService_Api>("PurchaseOrderService")
                .WithReference(databases.PurchaseOrder, "PurchaseOrderDbContext")
                .WaitFor(databases.PurchaseOrder)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/purchase-order/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var receiptService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ReceiptService_Api>("ReceiptService")
                .WithReference(databases.Receipt, "ReceiptDbContext")
                .WaitFor(databases.Receipt)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(invoiceService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/receipt/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var supplierService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_SupplierService_Api>("SupplierService")
                .WithReference(databases.Supplier, "SupplierDbContext")
                .WaitFor(databases.Supplier)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(purchaseOrderService)
                .WithReference(invoiceService)
                .WithReference(materialService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/supplier/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        purchaseOrderService = purchaseOrderService
            .WithReference(supplierService)
            .WithReference(orderService)
            .WithReference(currencyService)
            .WaitFor(supplierService)
            .WaitFor(orderService)
            .WaitFor(currencyService);

        var chatbotService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ChatbotService_Api>("ChatbotService")
                .WithReference(databases.Chatbot, "ChatbotDbContext")
                .WaitFor(databases.Chatbot)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithReference(countryService)
                .WithReference(registryService)
                .WithReference(uploadService)
                .WithReference(customerService)
                .WithReference(employeeService)
                .WithReference(authService)
                .WithReference(accountingService)
                .WithReference(notificationService)
                .WithReference(careerService)
                .WithReference(compensationService)
                .WithReference(complianceService)
                .WithReference(leaveService)
                .WithReference(lifecycleService)
                .WithReference(performanceService)
                .WithReference(contactService)
                .WithReference(currencyService)
                .WithReference(quotationService)
                .WithReference(invoiceService)
                .WithReference(materialService)
                .WithReference(pricingService)
                .WithReference(orderService)
                .WithReference(deliveryService)
                .WithReference(paymentService)
                .WithReference(pdfService)
                .WithReference(purchaseOrderService)
                .WithReference(receiptService)
                .WithReference(supplierService)
                .WithHttpHealthCheck("/chatbot/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var projectService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ProjectService_Api>("ProjectService")
                .WithReference(databases.Project, "ProjectDbContext")
                .WaitFor(databases.Project)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithReference(customerService)
                .WithReference(pricingService)
                .WithReference(quotationService)
                .WithReference(orderService)
                .WithReference(notificationService)
                .WithHttpHealthCheck("/project/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var searchService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_SearchService_Api>("SearchService")
                .WithReference(databases.Search, "SearchDbContext")
                .WaitFor(databases.Search)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithUrlForEndpoint("http", u => { u.Url = "/search/scalar"; u.DisplayText = "Scalar Documentation"; })
                .WithUrlForEndpoint("https", u => { u.Url = "/search/scalar"; u.DisplayText = "Scalar Documentation"; })
                .WithHttpHealthCheck("/search/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var intranetBff = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_Intranet_Bff>("IntranetBff")
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(databases.Intranet, "IntranetDbContext")
                .WaitFor(databases.Intranet)
                .WithReference(authService)
                .WithReference(customerService)
                .WithReference(orderService)
                .WithReference(deliveryService)
                .WithReference(iamService)
                .WithReference(countryService)
                .WithReference(registryService)
                .WithReference(uploadService)
                .WithReference(quotationService)
                .WithReference(materialService)
                .WithReference(employeeService)
                .WithReference(invoiceService)
                .WithReference(paymentService)
                .WithReference(pdfService)
                .WithReference(supplierService)
                .WithReference(chatbotService)
                .WithReference(careerService)
                .WithReference(complianceService)
                .WithReference(performanceService)
                .WithReference(compensationService)
                .WithReference(accountingService)
                .WithReference(contactService)
                .WithReference(receiptService)
                .WithReference(lifecycleService)
                .WithReference(purchaseOrderService)
                .WithReference(leaveService)
                .WithReference(pricingService)
                .WithReference(notificationService)
                .WithReference(facilityService)
                .WithReference(projectService)
                .WithReference(searchService)
                .WithReference(currencyService)
                .WithUrlForEndpoint("http", u => u.DisplayText = "Intranet (HTTP)")
                .WithUrlForEndpoint("https", u => u.DisplayText = "Intranet (HTTPS)")
                .WithHttpHealthCheck("/intranet/aspire-liveness")
                .WithHttpCommand(
                    path: "/api/v1/seed/customers",
                    displayName: "Seed Customer Data",
                    commandOptions: new HttpCommandOptions
                    {
                        IconName = "Database",
                        IconVariant = IconVariant.Filled,
                        IsHighlighted = true,
                        Description = "Seed Maliev customer data (Company, Customer, Addresses)"
                    }),
            config,
            grafana,
            otelCollector,
            environmentName);

        var inventoryService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_InventoryService_Api>("InventoryService")
                .WithReference(databases.Inventory, "InventoryDbContext")
                .WaitFor(databases.Inventory)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/inventory/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        var jobService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_JobService_Api>("JobService")
                .WithReference(databases.Job, "JobDbContext")
                .WaitFor(databases.Job)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithReference(orderService)
                .WithReference(facilityService)
                .WithReference(materialService)
                .WithReference(notificationService)
                .WaitFor(notificationService)
                .WithHttpHealthCheck("/job/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        intranetBff = intranetBff.WithReference(inventoryService);

        // Add JobService reference to IntranetBff now that jobService is declared
        intranetBff = intranetBff.WithReference(jobService);

        // PricingService queries JobService for queue depth — wire the reference here
        // because jobService is declared after pricingService.
        pricingService = pricingService.WithReference(jobService).WaitFor(jobService);

        var predictionService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PredictionService_Api>("PredictionService")
                .WithReference(databases.Prediction, "PredictionDatabase")
                .WaitFor(databases.Prediction)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis, "Redis")
                .WithReference(iamService)
                .WithHttpHealthCheck("/predictionservice/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

        intranetBff = intranetBff.WithReference(predictionService);

        // --- Geometry Service (Python FastAPI — Linux Docker container) ---
        //
        // GeometryService is a Python/FastAPI workload, not a .NET project. It runs inside a
        // Linux Docker container both locally (via Aspire) and in production (Kubernetes Engine
        // on Linux). Keeping local and production identical eliminates an entire class of
        // platform-specific bugs (e.g. Linux-only rendering libraries, OSMesa, headless Xvfb).
        //
        // AddDockerfile rebuilds the image from source on every debug start (F5), so there is
        // never a risk of running stale code. The build context is the Maliev.GeometryService
        // directory; the Dockerfile uses a two-stage python:3.12-slim build with Poetry.
        //
        // Because this is not a .NET project it does NOT go through WithSharedSecrets.
        // JWT keys and service URLs are injected directly as flat environment variables,
        // which is how pydantic-settings reads them on the Python side.
        var geometryService = builder.AddDockerfile("GeometryService", "../../Maliev.GeometryService")
            .WithReference(infrastructure.RabbitMQ)
            .WaitFor(infrastructure.RabbitMQ)
            .WithReference(uploadService)
            .WaitFor(uploadService)
            .WithEnvironment("RABBITMQ_URI", infrastructure.RabbitMQ)
            .WithEnvironment("UPLOAD_SERVICE_URL", uploadService.GetEndpoint("http"))
            .WithEnvironment("JWT_PRIVATE_KEY", config.JwtPrivateKey)
            .WithEnvironment("JWT_SECURITY_KEY", config.JwtSecurityKey)
            .WithEnvironment("JWT_ISSUER", config.JwtIssuer)
            .WithEnvironment("JWT_AUDIENCE", config.JwtAudience)
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otelCollector.GetEndpoint("grpc"))
            .WaitFor(otelCollector)
            .WithEnvironment("GEOMETRY_MAIN_WORKERS", "2")
            .WithEnvironment("GEOMETRY_DFM_WORKERS", "1")
            .WithEnvironment("GEOMETRY_PREVIEW_RENDER_WORKERS", "2")
            .WithEnvironment("GEOMETRY_DFM_BODY_WORKERS", "2")
            .WithEnvironment("GEOMETRY_FILE_INGEST_CONCURRENCY", "2")
            .WithEnvironment("GEOMETRY_ARTIFACT_CONCURRENCY", "2")
            .WithEnvironment("GEOMETRY_RABBITMQ_PREFETCH", "2")
            .WithExternalHttpEndpoints()
            .WithHttpEndpoint(port: 8081, targetPort: 8081, env: "PORT")
            .WithUrlForEndpoint("http", u => { u.Url = "/geometry/scalar"; u.DisplayText = "Scalar Documentation"; })
            .WithHttpHealthCheck("/geometry/aspire-liveness");

        // Wire GeometryService into IntranetBff for service discovery.
        // GeometryService is a Docker container (not a .NET project), so its endpoint is injected
        // via EndpointReference — which Aspire translates to the services__GeometryService__http__0
        // environment variable that AddServiceDiscovery() reads on the BFF side.
        intranetBff = intranetBff.WithReference(geometryService.GetEndpoint("http"));
    }

    /// <summary>
    /// Helper method to inject shared secrets and environment variables into a service.
    /// </summary>
    private static IResourceBuilder<ProjectResource> WithSharedSecrets(
        IResourceBuilder<ProjectResource> project,
        SharedConfiguration config,
        IResourceBuilder<ContainerResource> grafana,
        IResourceBuilder<ContainerResource> otelCollector,
        string environmentName)
    {
        return project
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", environmentName)
            .WithEnvironment("Jwt__PublicKey", config.JwtPublicKey)
            .WithEnvironment("Jwt__SecurityKey", config.JwtSecurityKey)
            .WithEnvironment("Jwt__PrivateKey", config.JwtPrivateKey)
            .WithEnvironment("Jwt__Issuer", config.JwtIssuer)
            .WithEnvironment("Jwt__Audience", config.JwtAudience)
            .WithEnvironment("Authentication__Google__ClientId", config.GoogleClientId)
            .WithEnvironment("Authentication__Google__ClientSecret", config.GoogleClientSecret)
            .WithEnvironment("CORS__AllowedOrigins", config.CorsAllowedOrigins)
            .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"))
            .WithEnvironment("Observability__TracingEnabled", "false")
            .WithEnvironment("Observability__RuntimeMetricsEnabled", "false")
            .WithEnvironment("DOTNET_gcServer", "0")
            .WithEnvironment("COMPlus_gcServer", "0")
            .WithEnvironment("DOTNET_GCHeapHardLimitPercent", "3")
            .WithEnvironment("COMPlus_GCHeapHardLimitPercent", "3")
            .WithEnvironment("NPGSQL_GSSAPI_AUTHENTICATION", "false")
            .WithEnvironment("PGGSSENCMODE", "disable");
    }
}

/// <summary>
/// Shared configuration values loaded from configuration sources.
/// </summary>
public record SharedConfiguration(
    IResourceBuilder<ParameterResource> JwtSecurityKey,
    IResourceBuilder<ParameterResource> JwtPrivateKey,
    IResourceBuilder<ParameterResource> JwtPublicKey,
    IResourceBuilder<ParameterResource> JwtIssuer,
    IResourceBuilder<ParameterResource> JwtAudience,
    IResourceBuilder<ParameterResource> GoogleClientId,
    IResourceBuilder<ParameterResource> GoogleClientSecret,
    IResourceBuilder<ParameterResource> AspireTestAdminEnabled,
    IResourceBuilder<ParameterResource> AspireTestAdminPassword,
    IResourceBuilder<ParameterResource> CorsAllowedOrigins,
    IResourceBuilder<ParameterResource> GcpProjectId,
    IResourceBuilder<ParameterResource> GcpServiceAccountKeyBase64,
    IResourceBuilder<ParameterResource> NotificationEncryptionKey);

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
    IResourceBuilder<PostgresDatabaseResource> Accounting,
    IResourceBuilder<PostgresDatabaseResource> Auth,
    IResourceBuilder<PostgresDatabaseResource> Career,
    IResourceBuilder<PostgresDatabaseResource> Chatbot,
    IResourceBuilder<PostgresDatabaseResource> Compensation,
    IResourceBuilder<PostgresDatabaseResource> Compliance,
    IResourceBuilder<PostgresDatabaseResource> Contact,
    IResourceBuilder<PostgresDatabaseResource> Country,
    IResourceBuilder<PostgresDatabaseResource> Currency,
    IResourceBuilder<PostgresDatabaseResource> Customer,
    IResourceBuilder<PostgresDatabaseResource> Delivery,
    IResourceBuilder<PostgresDatabaseResource> Employee,
    IResourceBuilder<PostgresDatabaseResource> IAM,
    IResourceBuilder<PostgresDatabaseResource> Intranet,
    IResourceBuilder<PostgresDatabaseResource> Invoice,
    IResourceBuilder<PostgresDatabaseResource> Leave,
    IResourceBuilder<PostgresDatabaseResource> Lifecycle,
    IResourceBuilder<PostgresDatabaseResource> Material,
    IResourceBuilder<PostgresDatabaseResource> Notification,
    IResourceBuilder<PostgresDatabaseResource> Order,
    IResourceBuilder<PostgresDatabaseResource> Payment,
    IResourceBuilder<PostgresDatabaseResource> Pdf,
    IResourceBuilder<PostgresDatabaseResource> Performance,
    IResourceBuilder<PostgresDatabaseResource> Prediction,
    IResourceBuilder<PostgresDatabaseResource> Pricing,
    IResourceBuilder<PostgresDatabaseResource> PurchaseOrder,
    IResourceBuilder<PostgresDatabaseResource> Quotation,
    IResourceBuilder<PostgresDatabaseResource> Receipt,
    IResourceBuilder<PostgresDatabaseResource> Registry,
    IResourceBuilder<PostgresDatabaseResource> Search,
    IResourceBuilder<PostgresDatabaseResource> Supplier,
    IResourceBuilder<PostgresDatabaseResource> Upload,
    IResourceBuilder<PostgresDatabaseResource> Facility,
    IResourceBuilder<PostgresDatabaseResource> Inventory,
    IResourceBuilder<PostgresDatabaseResource> Job,
    IResourceBuilder<PostgresDatabaseResource> Project);
