using Maliev.Aspire.DatabaseSeeder.Seeding.Services.CountryService;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.EmployeeService;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.IAMService;
using Maliev.Aspire.AppHost;
using Maliev.Aspire.AppHost.Extensions;
using Maliev.Aspire.AppHost.OpenTelemetryCollector;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

// Disable GSSAPI negotiation globally for the AppHost process and its probes.
// This silences the "SPNEGO cannot find mechanisms to negotiate" logs in postgres-server.
Environment.SetEnvironmentVariable("NPGSQL_GSSAPI_AUTHENTICATION", "false");
Environment.SetEnvironmentVariable("PGGSSENCMODE", "disable");

var builder = DistributedApplication.CreateBuilder(args);

var config = Program.LoadSharedConfiguration(builder);

// ──────────────────────────────────────────────
// 1. Frontends — registered first for dashboard display order
// ──────────────────────────────────────────────
var intranetBff = builder.AddProject<Projects.Maliev_Intranet_Bff>("IntranetBff")
    .WithUrlForEndpoint("http", u => u.DisplayText = "Intranet (HTTP)")
    .WithUrlForEndpoint("https", u => u.DisplayText = "Intranet (HTTPS)")
    .WithTestingSafeHttpHealthCheck("/intranet/aspire-liveness");

var quoteEngineBff = builder.AddProject<Projects.Maliev_QuoteEngine_Bff>("QuoteEngineBff")
    .WithUrlForEndpoint("http", u => u.DisplayText = "Quote Engine (HTTP)")
    .WithUrlForEndpoint("https", u => u.DisplayText = "Quote Engine (HTTPS)")
    .WithTestingSafeHttpHealthCheck("/quote/aspire-liveness");

var webBff = builder.AddProject<Projects.Maliev_Web_Bff>("WebBff")
    .WithUrlForEndpoint("http", u => u.DisplayText = "Customer Web (HTTP)")
    .WithUrlForEndpoint("https", u => u.DisplayText = "Customer Web (HTTPS)")
    .WithTestingSafeHttpHealthCheck("/web/aspire-liveness");

// ──────────────────────────────────────────────
// 2. Infrastructure
// ──────────────────────────────────────────────
var infrastructure = Program.ConfigureInfrastructure(builder);
var databases = Program.ConfigureDatabases(infrastructure.Postgres);

// ──────────────────────────────────────────────
// 3. Monitoring (Prometheus, Grafana, OpenTelemetry)
// ──────────────────────────────────────────────
var prometheus = ConfigurePrometheus(builder);
var grafana = ConfigureGrafana(builder, prometheus);
var otelCollector = ConfigureOpenTelemetry(builder, prometheus);

// ──────────────────────────────────────────────
// 4. Services + wire frontends internally
// ──────────────────────────────────────────────
Program.ConfigureServices(
    builder, infrastructure, databases, config, grafana, otelCollector,
    intranetBff, quoteEngineBff, webBff);

builder.Build().Run();

// --- Local Infrastructure Configuration Functions ---

static IResourceBuilder<ContainerResource> ConfigurePrometheus(IDistributedApplicationBuilder builder)
{
    return builder.AddContainer("prometheus", "prom/prometheus", "v3.0.1")
        .WithContainerFiles("/etc/prometheus", AppHostPathResolver.ResolveRequiredDirectoryPath("../prometheus"))
        .WithArgs("--web.enable-otlp-receiver", "--config.file=/etc/prometheus/prometheus.yml")
        .WithHttpEndpoint(targetPort: 9090)
        .WithUrlForEndpoint("http", u => u.DisplayText = "Prometheus Dashboard")
        .WithContainerRuntimeArgs("--cpus", "0.5", "--memory", "384m");
}

static IResourceBuilder<ContainerResource> ConfigureGrafana(
    IDistributedApplicationBuilder builder,
    IResourceBuilder<ContainerResource> prometheus)
{
    return builder.AddContainer("grafana", "grafana/grafana")
        .WithContainerFiles("/etc/grafana", AppHostPathResolver.ResolveRequiredDirectoryPath("../grafana/config"))
        .WithContainerFiles("/var/lib/grafana/dashboards", AppHostPathResolver.ResolveRequiredDirectoryPath("../grafana/dashboards"))
        .WithEnvironment("PROMETHEUS_ENDPOINT", prometheus.GetEndpoint("http"))
        .WithHttpEndpoint(targetPort: 3000)
        .WithUrlForEndpoint("http", u => u.DisplayText = "Grafana Dashboard")
        .WithContainerRuntimeArgs("--cpus", "0.5", "--memory", "256m");
}

static IResourceBuilder<ContainerResource> ConfigureOpenTelemetry(
    IDistributedApplicationBuilder builder,
    IResourceBuilder<ContainerResource> prometheus)
{
    return builder.AddOpenTelemetryCollector("otelcollector", "../otelcollector/config.yaml")
        .WithEnvironment("PROMETHEUS_ENDPOINT", $"{prometheus.GetEndpoint("http")}/api/v1/otlp")
        .WithContainerRuntimeArgs("--cpus", "0.5", "--memory", "256m");
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
        var webGoogleClientId = builder.AddParameterFromConfig(
            "WebGoogleClientId",
            "Authentication:Google:Web:ClientId",
            secret: true,
            defaultValue: builder.Configuration["Authentication:Google:ClientId"]);
        var webGoogleClientSecret = builder.AddParameterFromConfig(
            "WebGoogleClientSecret",
            "Authentication:Google:Web:ClientSecret",
            secret: true,
            defaultValue: builder.Configuration["Authentication:Google:ClientSecret"]);

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

        var webGoogleMapsApiKey = builder.AddParameterFromConfig("WebGoogleMapsApiKey", "GoogleMaps:BrowserApiKey", secret: true);

        var businessRegistryDdbApiKey = builder.AddParameterFromConfig("BusinessRegistryDdbApiKey", "BusinessRegistry:DdbApiKey", secret: true);

        var omisePublicKey = builder.AddParameterFromConfig("OmisePublicKey", "PaymentProviders:Omise:PublicKey", secret: true);
        var omiseSecretKey = builder.AddParameterFromConfig("OmiseSecretKey", "PaymentProviders:Omise:SecretKey", secret: true);
        var omiseWebhookSecret = builder.AddParameterFromConfig(
            "OmiseWebhookSecret",
            "PaymentProviders:Omise:WebhookSecret",
            secret: true,
            defaultValue: "whsec_omise_development_secret");

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
            WebGoogleClientId: webGoogleClientId,
            WebGoogleClientSecret: webGoogleClientSecret,
            AspireTestAdminEnabled: aspireTestAdminEnabled,
            AspireTestAdminPassword: aspireTestAdminPassword,
            CorsAllowedOrigins: corsAllowedOrigins,
            GcpProjectId: gcpProjectId,
            GcpServiceAccountKeyBase64: gcpServiceAccountKeyBase64,
            OmisePublicKey: omisePublicKey,
            OmiseSecretKey: omiseSecretKey,
            OmiseWebhookSecret: omiseWebhookSecret,
            NotificationEncryptionKey: notificationEncryptionKey,
            WebGoogleMapsApiKey: webGoogleMapsApiKey,
            BusinessRegistryDdbApiKey: businessRegistryDdbApiKey
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
                .WithUrlForEndpoint("management", u => u.DisplayText = "RabbitMQ Management")
                .WithContainerRuntimeArgs("--cpus", "1", "--memory", "512m");
        }

        static IResourceBuilder<RedisResource> ConfigureRedis(IDistributedApplicationBuilder builder)
        {
            var redis = builder.AddRedis("redis")
                .WithImageTag("8.4-alpine")
                .WithContainerRuntimeArgs("--cpus", "0.5", "--memory", "256m");

            redis.WithRedisInsight(insight =>
            {
                insight.WithVolume("redisinsight-data", "/data")
                    .WithUrlForEndpoint("http", u => u.DisplayText = "RedisInsight Dashboard")
                    .WithContainerRuntimeArgs("--cpus", "0.5", "--memory", "256m");
            });

            return redis;
        }

        static IResourceBuilder<PostgresServerResource> ConfigurePostgres(IDistributedApplicationBuilder builder)
        {
            return builder.AddPostgres("postgres-server")
                .WithImageTag("18-alpine")
                // Local-dev tuning: shared_buffers 512MB→128MB (Postgres default; the OS page
                // cache covers the rest) and a 2GB container ceiling (was 4GB). Postgres idles
                // ~400MB even with all 37 databases, so this frees headroom inside the capped
                // WSL VM for the rest of the always-on stack without risking OOM under seeding.
                .WithArgs("-c", "max_connections=150", "-c", "shared_buffers=128MB")
                .WithContainerRuntimeArgs("--cpus", "2", "--memory", "2048m")
                .WithEnvironment("PGGSSENCMODE", "disable") // Disable GSSAPI for internal container probes (pg_isready)
                .WithPgAdmin(option =>
                {
                    option.WithImageTag("9.11")
                        .WithEnvironment("PGGSSENCMODE", "disable") // Disable GSSAPI for pgAdmin connections
                        .WithEnvironment("PYTHONWARNINGS", "ignore") // Suppress SyntaxWarnings from sshtunnel in Python 3.14+
                        .WithUrlForEndpoint("http", u => u.DisplayText = "pgAdmin Dashboard")
                        .WithContainerRuntimeArgs("--cpus", "0.5", "--memory", "256m");
                });
        }
    }

    private static IResourceBuilder<ParameterResource> AddParameterFromConfig(
        this IDistributedApplicationBuilder builder,
        string parameterName,
        string configKey,
        bool secret = false,
        string? defaultValue = null)
    {
        var configuredValue = builder.Configuration[configKey];

        if (string.IsNullOrWhiteSpace(configuredValue) && defaultValue is not null)
        {
            return builder.AddParameter(parameterName, defaultValue, secret: secret);
        }

        var parameter = builder.AddParameter(parameterName, secret: secret);
        builder.Configuration[$"Parameters:{parameterName}"] = configuredValue;
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
            Commerce: postgres.AddDatabase("commerce-app-db"),
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
        IResourceBuilder<ContainerResource> otelCollector,
        IResourceBuilder<ProjectResource> intranetBff,
        IResourceBuilder<ProjectResource> quoteEngineBff,
        IResourceBuilder<ProjectResource> webBff)
    {
        var environmentName = builder.Environment.EnvironmentName;

        void ConfigureAspireTestAdminSeeder(IResourceBuilder<ExecutableResource> seeder)
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
                .WithTestingSafeHttpHealthCheck("/iam/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/country/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName)
            .SeedDatabase<CountryDatabaseSeeder>(databases.Country);

        var registryService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_RegistryService_Api>("RegistryService")
                .WithReference(databases.Registry, "RegistryDbContext")
                .WaitFor(databases.Registry)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/registry/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/facility/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/upload/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName)
            .WithEnvironment("GoogleCloud__Enabled", "false")
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
                .WaitFor(countryService)
                .WithReference(uploadService)
                .WaitFor(uploadService)
                .WithReference(iamService)
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/customer/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/employee/aspire-liveness"),
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
                .WaitFor(customerService)
                .WithReference(employeeService)
                .WaitFor(employeeService)
                .WithReference(iamService)
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/auth/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName)
            .WithEnvironment("WebAuthn__RPId", "localhost")
            .WithEnvironment("WebAuthn__AllowedOrigins", "https://localhost:56139");

        // --- Business Services ---
        var accountingService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_AccountingService_Api>("AccountingService")
                .WithReference(databases.Accounting, "AccountingDbContext")
                .WaitFor(databases.Accounting)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/accounting/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/notification/aspire-liveness")
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/career/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/compensation/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/compliance/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/leave/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/lifecycle/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/performance/aspire-liveness"),
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
                .WithReference(countryService)
                .WithReference(iamService)
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/contact/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/currency/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithReference(customerService)
                .WithTestingSafeHttpHealthCheck("/quotation/aspire-liveness"),
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
                .WithReference(customerService)
                .WithReference(iamService)
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/invoice/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/material/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/pricing/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/order/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/delivery/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/payment/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName)
            .WithEnvironment("PaymentProviders__Omise__PublicKey", config.OmisePublicKey)
            .WithEnvironment("PaymentProviders__Omise__SecretKey", config.OmiseSecretKey)
            .WithEnvironment("PaymentProviders__Omise__WebhookSecret", config.OmiseWebhookSecret)
            .WithEnvironment("PaymentProviders__Omise__ApiBaseUrl", "https://api.omise.co");

        var commerceService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_CommerceService_Api>("CommerceService")
                .WithReference(databases.Commerce, "CommerceDbContext")
                .WaitFor(databases.Commerce)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WaitFor(iamService)
                .WithReference(customerService)
                .WithTestingSafeHttpHealthCheck("/commerce/aspire-liveness"),
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
                .WithReference(invoiceService)
                .WithReference(iamService)
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/pdf/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/purchase-order/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/receipt/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/supplier/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithReference(countryService)
                .WaitFor(countryService)
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
                // QuoteEngine BFF discovery so ChatbotService can call back the Quote Agent
                // tool endpoint (/quote/v1/agent/tools/*). No WaitFor: quoteEngineBff already
                // references chatbotService, so a WaitFor here would create a startup cycle.
                .WithReference(quoteEngineBff)
                .WithTestingSafeHttpHealthCheck("/chatbot/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName)
            .WithEnvironment("Chatbot__AllowedThinkingCallbackOrigins__0", quoteEngineBff.GetEndpoint("https"));

        var projectService = WithSharedSecrets(
            builder.AddProject<Projects.Maliev_ProjectService_Api>("ProjectService")
                .WithReference(databases.Project, "ProjectDbContext")
                .WaitFor(databases.Project)
                .WithReference(infrastructure.RabbitMQ)
                .WaitFor(infrastructure.RabbitMQ)
                .WithReference(infrastructure.Redis)
                .WithReference(iamService)
                .WaitFor(iamService)
                .WithReference(customerService)
                .WithReference(pricingService)
                .WithReference(quotationService)
                .WithReference(orderService)
                .WithReference(notificationService)
                .WithTestingSafeHttpHealthCheck("/project/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/search/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithReference(materialService)
                .WithTestingSafeHttpHealthCheck("/inventory/aspire-liveness"),
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
                .WaitFor(iamService)
                .WithReference(orderService)
                .WithReference(facilityService)
                .WithReference(materialService)
                .WithReference(notificationService)
                .WaitFor(notificationService)
                .WithTestingSafeHttpHealthCheck("/job/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

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
                .WaitFor(iamService)
                .WithTestingSafeHttpHealthCheck("/predictionservice/aspire-liveness"),
            config,
            grafana,
            otelCollector,
            environmentName);

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
            .WithEnvironment("JWT_PUBLIC_KEY", config.JwtPublicKey)
            .WithEnvironment("JWT_SECURITY_KEY", config.JwtSecurityKey)
            .WithEnvironment("JWT_ISSUER", config.JwtIssuer)
            .WithEnvironment("JWT_AUDIENCE", config.JwtAudience)
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otelCollector.GetEndpoint("grpc"));

        if (!environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase))
        {
            geometryService = geometryService.WaitFor(otelCollector);
        }

        geometryService = geometryService
            .WithEnvironment("GEOMETRY_MAIN_WORKERS", "1")
            .WithEnvironment("GEOMETRY_DFM_WORKERS", "1")
            .WithEnvironment("GEOMETRY_PREVIEW_RENDER_WORKERS", "1")
            .WithEnvironment("GEOMETRY_DFM_BODY_WORKERS", "1")
            .WithEnvironment("GEOMETRY_FILE_INGEST_CONCURRENCY", "1")
            .WithEnvironment("GEOMETRY_ARTIFACT_CONCURRENCY", "1")
            .WithEnvironment("GEOMETRY_RABBITMQ_PREFETCH", "1")
            .WithExternalHttpEndpoints()
            .WithHttpEndpoint(targetPort: 8081, env: "PORT")
            .WithUrlForEndpoint("http", u => { u.Url = "/geometry/scalar"; u.DisplayText = "Geometry Scalar"; })
            .WithTestingSafeHttpHealthCheck("/geometry/aspire-liveness")
            .WithContainerRuntimeArgs("--cpus", "2", "--memory", "2048m");

        // Wire GeometryService into BFFs for service discovery.
        // GeometryService is a Docker container (not a .NET project), so its endpoint is injected
        // via EndpointReference — which Aspire translates to the services__GeometryService__http__0
        // environment variable that AddServiceDiscovery() reads on the BFF side.
        intranetBff
            .WithReference(geometryService.GetEndpoint("http"))
            .WithEnvironment("SystemHealth__ProbeTimeouts__GeometryService__LivenessSeconds", "20")
            .WithEnvironment("SystemHealth__ProbeTimeouts__GeometryService__ReadinessSeconds", "20");
        quoteEngineBff.WithReference(geometryService.GetEndpoint("http"));

        // ──────────────────────────────────────────────
        // Frontend service wiring (infrastructure + service references)
        // ──────────────────────────────────────────────
        intranetBff
            .WithReference(infrastructure.RabbitMQ).WaitFor(infrastructure.RabbitMQ)
            .WithReference(infrastructure.Redis)
            .WithReference(databases.Intranet, "IntranetDbContext").WaitFor(databases.Intranet)
            .WithReference(authService)
            .WithReference(customerService)
            .WithReference(orderService)
            .WithReference(deliveryService)
            .WithReference(iamService).WaitFor(iamService)
            .WithReference(countryService).WaitFor(countryService)
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
            .WithReference(commerceService)
            .WithReference(inventoryService)
            .WithReference(jobService)
            .WithReference(predictionService)
            .WithHttpCommand(
                path: "/api/v1/seed/customers",
                displayName: "Seed Customer Data",
                commandOptions: new HttpCommandOptions
                {
                    IconName = "Database",
                    IconVariant = IconVariant.Filled,
                    IsHighlighted = true,
                    Description = "Seed Maliev customer data (Company, Customer, Addresses)",
                    PrepareRequest = context =>
                    {
                        var tokenProvider = new ServiceAccountTokenProvider(builder.Configuration, "IntranetBff");
                        context.Request.Headers.Authorization =
                            new AuthenticationHeaderValue("Bearer", tokenProvider.GetToken());

                        return Task.CompletedTask;
                    }
                });

        quoteEngineBff
            .WithReference(infrastructure.RabbitMQ).WaitFor(infrastructure.RabbitMQ)
            .WithReference(infrastructure.Redis)
            .WithReference(authService)
            .WithReference(iamService).WaitFor(iamService)
            .WithReference(customerService)
            .WithReference(registryService)
            .WithReference(uploadService)
            .WithReference(materialService)
            .WithReference(pricingService)
            .WithReference(projectService)
            .WithReference(quotationService)
            .WithReference(invoiceService)
            .WithReference(pdfService)
            .WithReference(orderService)
            .WithReference(paymentService)
            .WithReference(deliveryService)
            .WithReference(chatbotService)
            .WithReference(currencyService);

        webBff
            .WithReference(authService).WaitFor(authService)
            .WithReference(iamService).WaitFor(iamService)
            .WithReference(customerService).WaitFor(customerService)
            .WithReference(countryService)
            .WithReference(registryService)
            .WithReference(contactService)
            .WithReference(deliveryService)
            .WithReference(materialService)
            .WithReference(orderService)
            .WithReference(paymentService)
            .WithReference(pricingService)
            .WithReference(uploadService)
            .WithReference(commerceService)
            .WithReference(chatbotService)
            .WithReference(pdfService);

        // ──────────────────────────────────────────────
        // Apply shared secrets to frontends
        // ──────────────────────────────────────────────
        intranetBff = WithSharedSecrets(intranetBff, config, grafana, otelCollector, environmentName);
        quoteEngineBff = WithSharedSecrets(quoteEngineBff, config, grafana, otelCollector, environmentName)
            .WithEnvironment("Web__BaseUrl", webBff.GetEndpoint("https"))
            .WithEnvironment("QuoteAgent__EnableThinkingCallbacks", "true")
            .WithEnvironment("QuoteAgent__ThinkingCallbackBaseUrl", quoteEngineBff.GetEndpoint("https"))
            .WithEnvironment("GoogleMaps__BrowserApiKey", config.WebGoogleMapsApiKey);
        webBff = WithSharedSecrets(webBff, config, grafana, otelCollector, environmentName)
            .WithEnvironment("QuoteEngine__BaseUrl", quoteEngineBff.GetEndpoint("https"))
            .WithEnvironment("Authentication__Google__ClientId", config.WebGoogleClientId)
            .WithEnvironment("Authentication__Google__ClientSecret", config.WebGoogleClientSecret)
            .WithEnvironment("GoogleMaps__BrowserApiKey", config.WebGoogleMapsApiKey)
            .WithEnvironment("BusinessRegistry__DdbApiKey", config.BusinessRegistryDdbApiKey);
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
            .WithServiceScalarUrl()
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", environmentName)
            .WithEnvironment("DOTNET_ENVIRONMENT", environmentName)
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
            // Cap each service's GC heap at 2% of host RAM (~636 MB on 31.8 GB) so the
            // ~40 services starting in parallel don't collectively exceed physical RAM.
            .WithEnvironment("DOTNET_GCHeapHardLimitPercent", "2")
            .WithEnvironment("COMPlus_GCHeapHardLimitPercent", "2")
            // Conserve memory level 3 (scale 0–9): tells the GC to return freed pages
            // to the OS promptly rather than hoarding them. Prevents per-service RSS
            // from accumulating during the seeding burst; CPU cost at level 3 is minimal.
            .WithEnvironment("DOTNET_GCConserveMemory", "3")
            .WithEnvironment("COMPlus_GCConserveMemory", "3")
            .WithEnvironment("NPGSQL_GSSAPI_AUTHENTICATION", "false")
            .WithEnvironment("PGGSSENCMODE", "disable");
    }

    private static IResourceBuilder<ProjectResource> WithServiceScalarUrl(
        this IResourceBuilder<ProjectResource> project)
    {
        if (!ServiceScalarRoutes.TryGetValue(project.Resource.Name, out var route))
        {
            return project;
        }

        return project
            .WithUrlForEndpoint("http", url =>
            {
                url.Url = route.Path;
                url.DisplayText = route.DisplayText;
            })
            .WithUrlForEndpoint("https", url =>
            {
                url.Url = route.Path;
                url.DisplayText = route.DisplayText;
            });
    }

    private static readonly IReadOnlyDictionary<string, ScalarRoute> ServiceScalarRoutes =
        new Dictionary<string, ScalarRoute>(StringComparer.Ordinal)
        {
            ["AccountingService"] = new("/accounting/scalar", "Accounting Scalar"),
            ["AuthService"] = new("/auth/scalar", "Auth Scalar"),
            ["CareerService"] = new("/career/scalar", "Career Scalar"),
            ["ChatbotService"] = new("/chatbot/scalar", "Chatbot Scalar"),
            ["CommerceService"] = new("/commerce/scalar", "Commerce Scalar"),
            ["CompensationService"] = new("/compensation/scalar", "Compensation Scalar"),
            ["ComplianceService"] = new("/compliance/scalar", "Compliance Scalar"),
            ["ContactService"] = new("/contact/scalar", "Contact Scalar"),
            ["CountryService"] = new("/country/scalar", "Country Scalar"),
            ["CurrencyService"] = new("/currency/scalar", "Currency Scalar"),
            ["CustomerService"] = new("/customer/scalar", "Customer Scalar"),
            ["DeliveryService"] = new("/delivery/scalar", "Delivery Scalar"),
            ["EmployeeService"] = new("/employee/scalar", "Employee Scalar"),
            ["FacilityService"] = new("/facility/scalar", "Facility Scalar"),
            ["IAMService"] = new("/iam/scalar", "IAM Scalar"),
            ["InventoryService"] = new("/inventory/scalar", "Inventory Scalar"),
            ["InvoiceService"] = new("/invoice/scalar", "Invoice Scalar"),
            ["JobService"] = new("/job/scalar", "Job Scalar"),
            ["LeaveService"] = new("/leave/scalar", "Leave Scalar"),
            ["LifecycleService"] = new("/lifecycle/scalar", "Lifecycle Scalar"),
            ["MaterialService"] = new("/material/scalar", "Material Scalar"),
            ["NotificationService"] = new("/notification/scalar", "Notification Scalar"),
            ["OrderService"] = new("/order/scalar", "Order Scalar"),
            ["PaymentService"] = new("/payment/scalar", "Payment Scalar"),
            ["PdfService"] = new("/pdf/scalar", "PDF Scalar"),
            ["PerformanceService"] = new("/performance/scalar", "Performance Scalar"),
            ["PredictionService"] = new("/predictionservice/scalar", "Prediction Scalar"),
            ["PricingService"] = new("/pricing/scalar", "Pricing Scalar"),
            ["ProjectService"] = new("/project/scalar", "Project Scalar"),
            ["PurchaseOrderService"] = new("/purchase-order/scalar", "Purchase Order Scalar"),
            ["QuotationService"] = new("/quotation/scalar", "Quotation Scalar"),
            ["ReceiptService"] = new("/receipt/scalar", "Receipt Scalar"),
            ["RegistryService"] = new("/registry/scalar", "Registry Scalar"),
            ["SearchService"] = new("/search/scalar", "Search Scalar"),
            ["SupplierService"] = new("/supplier/scalar", "Supplier Scalar"),
            ["UploadService"] = new("/upload/scalar", "Upload Scalar")
        };
}

internal sealed record ScalarRoute(string Path, string DisplayText);

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
    IResourceBuilder<ParameterResource> WebGoogleClientId,
    IResourceBuilder<ParameterResource> WebGoogleClientSecret,
    IResourceBuilder<ParameterResource> AspireTestAdminEnabled,
    IResourceBuilder<ParameterResource> AspireTestAdminPassword,
    IResourceBuilder<ParameterResource> CorsAllowedOrigins,
    IResourceBuilder<ParameterResource> GcpProjectId,
    IResourceBuilder<ParameterResource> GcpServiceAccountKeyBase64,
    IResourceBuilder<ParameterResource> OmisePublicKey,
    IResourceBuilder<ParameterResource> OmiseSecretKey,
    IResourceBuilder<ParameterResource> OmiseWebhookSecret,
    IResourceBuilder<ParameterResource> NotificationEncryptionKey,
    IResourceBuilder<ParameterResource> WebGoogleMapsApiKey,
    IResourceBuilder<ParameterResource> BusinessRegistryDdbApiKey);

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
    IResourceBuilder<PostgresDatabaseResource> Commerce,
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
