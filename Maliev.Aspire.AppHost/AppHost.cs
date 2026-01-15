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

        var corsAllowedOrigins = builder.AddParameter("CorsAllowedOrigins");
        // Convert the JSON array to a comma-separated string for easier environment injection
        var origins = builder.Configuration.GetSection("CORS:AllowedOrigins").Get<string[]>();
        builder.Configuration["Parameters:CorsAllowedOrigins"] = origins != null ? string.Join(",", origins) : string.Empty;

        return new SharedConfiguration(
            JwtSecurityKey: jwtSecurityKey,
            JwtPrivateKey: jwtPrivateKey,
            JwtPublicKey: jwtPublicKey,
            JwtIssuer: jwtIssuer,
            JwtAudience: jwtAudience,
            GoogleClientId: googleClientId,
            GoogleClientSecret: googleClientSecret,
            CorsAllowedOrigins: corsAllowedOrigins
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
            Employee: postgres.AddDatabase("employee-app-db"),
            IAM: postgres.AddDatabase("iam-app-db"),
            Invoice: postgres.AddDatabase("invoice-app-db"),
            Leave: postgres.AddDatabase("leave-app-db"),
            Lifecycle: postgres.AddDatabase("lifecycle-app-db"),
            Material: postgres.AddDatabase("material-app-db"),
            Notification: postgres.AddDatabase("notification-app-db"),
            Order: postgres.AddDatabase("order-app-db"),
            Payment: postgres.AddDatabase("payment-app-db"),
            Pdf: postgres.AddDatabase("pdf-app-db"),
            Performance: postgres.AddDatabase("performance-app-db"),
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
        IResourceBuilder<ContainerResource> grafana,
        IResourceBuilder<IResource> otelCollector)
    {
        // --- Core Services (dependencies for Auth) ---
        var iamService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_IAMService_Api>("maliev-iamservice-api")
                .WithReference(databases.IAM, "IamDbContext")
                .WaitFor(databases.IAM)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithHttpHealthCheck("/iam/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        // Note: CountryService must be declared before CustomerService to be referenced
        var countryService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CountryService_Api>("maliev-countryservice-api")
                .WithReference(databases.Country, "CountryDbContext")
                .WaitFor(databases.Country)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/country/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var uploadService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_UploadService_Api>("maliev-uploadservice-api")
                .WithReference(databases.Upload, "UploadDbContext")
                .WaitFor(databases.Upload)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/upload/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var customerService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CustomerService_Api>("maliev-customerservice-api")
                .WithReference(databases.Customer, "CustomerDbContext")
                .WaitFor(databases.Customer)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(countryService)
                .WithReference(uploadService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/customer/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var employeeService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_EmployeeService_Api>("maliev-employeeservice-api")
                .WithReference(databases.Employee, "EmployeeDbContext")
                .WaitFor(databases.Employee)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/employee/aspire-liveness"),
            config,
            grafana,
            otelCollector);

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
                .WithHttpHealthCheck("/auth/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        // --- Business Services ---
        var accountingService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_AccountingService_Api>("maliev-accountingservice-api")
                .WithReference(databases.Accounting, "AccountingDbContext")
                .WaitFor(databases.Accounting)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/accounting/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var chatbotService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ChatbotService_Api>("maliev-chatbotservice-api")
                .WithReference(databases.Chatbot, "ChatbotDbContext")
                .WaitFor(databases.Chatbot)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/chatbot/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var notificationService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_NotificationService_Api>("maliev-notificationservice-api")
                .WithReference(databases.Notification, "NotificationDbContext")
                .WaitFor(databases.Notification)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/notification/aspire-liveness"),
            config,
            grafana,
            otelCollector);

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
                .WithHttpHealthCheck("/career/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var compensationService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CompensationService_Api>("maliev-compensationservice-api")
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
            otelCollector);

        var complianceService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ComplianceService_Api>("maliev-complianceservice-api")
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
            otelCollector);

        var leaveService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_LeaveService_Api>("maliev-leaveservice-api")
                .WithReference(databases.Leave, "LeaveDbContext")
                .WaitFor(databases.Leave)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(employeeService)
                .WithReference(notificationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/leave/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var lifecycleService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_LifecycleService_Api>("maliev-lifecycleservice-api")
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
            otelCollector);

        var performanceService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PerformanceService_Api>("maliev-performanceservice-api")
                .WithReference(databases.Performance, "PerformanceDbContext")
                .WaitFor(databases.Performance)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(employeeService)
                .WithReference(notificationService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/performance/aspire-liveness"),
            config,
            grafana,
            otelCollector);

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
                .WithHttpHealthCheck("/contact/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var currencyService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CurrencyService_Api>("maliev-currencyservice-api")
                .WithReference(databases.Currency, "CurrencyDbContext")
                .WaitFor(databases.Currency)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/currency/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var quotationService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_QuotationService_Api>("maliev-quotationservice-api")
                .WithReference(databases.Quotation, "QuotationDbContext")
                .WaitFor(databases.Quotation)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/quotation/aspire-liveness"),
            config,
            grafana,
            otelCollector);

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
                .WithHttpHealthCheck("/invoice/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var materialService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_MaterialService_Api>("maliev-materialservice-api")
                .WithReference(databases.Material, "MaterialDbContext")
                .WaitFor(databases.Material)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/material/aspire-liveness"),
            config,
            grafana,
            otelCollector);

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
                .WithHttpHealthCheck("/order/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var paymentService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PaymentService_Api>("maliev-paymentservice-api")
                .WithReference(databases.Payment, "PaymentDbContext")
                .WaitFor(databases.Payment)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/payment/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var pdfService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PdfService_Api>("maliev-pdfservice-api")
                .WithReference(databases.Pdf, "PdfDbContext")
                .WaitFor(databases.Pdf)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(uploadService)
                .WithReference(iamService)
                .WithHttpHealthCheck("/pdf/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var purchaseOrderService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_PurchaseOrderService_Api>("maliev-purchaseorderservice-api")
                .WithReference(databases.PurchaseOrder, "PurchaseOrderDbContext")
                .WaitFor(databases.PurchaseOrder)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WithHttpHealthCheck("/purchase-order/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var receiptService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ReceiptService_Api>("maliev-receiptservice-api")
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
            otelCollector);

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
                .WithHttpHealthCheck("/supplier/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        var intranetBff = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_Intranet_Bff>("maliev-intranet-bff")
                .WithReference(authService)
                .WithReference(customerService)
                .WithReference(orderService)
                .WithReference(iamService)
                .WithUrlForEndpoint("http", u => u.DisplayText = "Intranet (HTTP)")
                .WithUrlForEndpoint("https", u => u.DisplayText = "Intranet (HTTPS)")
                .WithHttpHealthCheck("/intranet/aspire-liveness"),
            config,
            grafana,
            otelCollector);

        // --- Python Services ---
        var geometryService = builder.AddPythonApp("maliev-geometryservice", "../../Maliev.GeometryService", "src/main.py")
            .WithReference(infrastructure.RabbitMQ)
            .WaitFor(infrastructure.RabbitMQ)
            .WithEnvironment("RABBITMQ_URI", infrastructure.RabbitMQ)
            .WithHttpEndpoint(targetPort: 8080, name: "http")
            .WithUrlForEndpoint("http", u => { u.Url = "/geometry/scalar"; u.DisplayText = "Scalar Documentation"; })
            .WithHttpHealthCheck("/geometry/aspire-liveness")
            .WithVirtualEnvironment(".venv");
    }

    /// <summary>
    /// Helper method to inject shared secrets and environment variables into a service.
    /// </summary>
    private static IResourceBuilder<ProjectResource> WithSharedSecrets(
        IResourceBuilder<ProjectResource> project,
        SharedConfiguration config,
        IResourceBuilder<ContainerResource> grafana,
        IResourceBuilder<IResource> otelCollector)
    {
        return project
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("Jwt__PublicKey", config.JwtPublicKey)
            .WithEnvironment("Jwt__SecurityKey", config.JwtSecurityKey)
            .WithEnvironment("Jwt__PrivateKey", config.JwtPrivateKey)  // RSA private key for JWT signing
            .WithEnvironment("Authentication__Google__ClientId", config.GoogleClientId)
            .WithEnvironment("Authentication__Google__ClientSecret", config.GoogleClientSecret)
            .WithEnvironment("Jwt__Issuer", config.JwtIssuer)
            .WithEnvironment("Jwt__Audience", config.JwtAudience)
            .WithEnvironment("CORS__AllowedOrigins", config.CorsAllowedOrigins)
            .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"))
            .WithEnvironment("NPGSQL_GSSAPI_AUTHENTICATION", "false")
            .WithEnvironment("PGGSSENCMODE", "disable");
    }
}

/// <summary>
/// Shared configuration values loaded from configuration sources.
/// </summary>
record SharedConfiguration(
    IResourceBuilder<ParameterResource> JwtSecurityKey,
    IResourceBuilder<ParameterResource> JwtPrivateKey,
    IResourceBuilder<ParameterResource> JwtPublicKey,
    IResourceBuilder<ParameterResource> JwtIssuer,
    IResourceBuilder<ParameterResource> JwtAudience,
    IResourceBuilder<ParameterResource> GoogleClientId,
    IResourceBuilder<ParameterResource> GoogleClientSecret,
    IResourceBuilder<ParameterResource> CorsAllowedOrigins);

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
    IResourceBuilder<PostgresDatabaseResource> Employee,
    IResourceBuilder<PostgresDatabaseResource> IAM,
    IResourceBuilder<PostgresDatabaseResource> Invoice,
    IResourceBuilder<PostgresDatabaseResource> Leave,
    IResourceBuilder<PostgresDatabaseResource> Lifecycle,
    IResourceBuilder<PostgresDatabaseResource> Material,
    IResourceBuilder<PostgresDatabaseResource> Notification,
    IResourceBuilder<PostgresDatabaseResource> Order,
    IResourceBuilder<PostgresDatabaseResource> Payment,
    IResourceBuilder<PostgresDatabaseResource> Pdf,
    IResourceBuilder<PostgresDatabaseResource> Performance,
    IResourceBuilder<PostgresDatabaseResource> PurchaseOrder,
    IResourceBuilder<PostgresDatabaseResource> Quotation,
    IResourceBuilder<PostgresDatabaseResource> Receipt,
    IResourceBuilder<PostgresDatabaseResource> Supplier,
    IResourceBuilder<PostgresDatabaseResource> Upload);
