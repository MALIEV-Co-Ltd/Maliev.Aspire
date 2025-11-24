using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Load shared secrets from sharedsecrets.json and user secrets
builder.Configuration.AddJsonFile("sharedsecrets.json", optional: true);

// --- Shared Secrets Configuration ---
// These will be injected into all services via environment variables
var jwtPublicKey = builder.Configuration["Jwt:PublicKey"] ?? "";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "";
var corsAllowedOrigins = builder.Configuration["CORS:AllowedOrigins"] ?? "";

// --- Messaging and Caching ---
var erlangCookie = builder.Configuration["RabbitMQ:ErlangCookie"];
if (string.IsNullOrEmpty(erlangCookie))
{
    throw new InvalidOperationException("RabbitMQ ErlangCookie is not configured. Please add 'RabbitMQ:ErlangCookie' to sharedsecrets.json or another configuration source.");
}
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
                      .WithEnvironment("RABBITMQ_ERLANG_COOKIE", erlangCookie);

var redis = builder.AddRedis("redis");

// --- PostgreSQL Database Server ---
var postgres = builder.AddPostgres("postgres-server")
                      .WithImageTag("18")
                      .WithPgAdmin();

// --- Databases for Each Service (using -app-db pattern) ---
var authDb = postgres.AddDatabase("auth-app-db");
var careerDb = postgres.AddDatabase("career-app-db");
var contactDb = postgres.AddDatabase("contact-app-db");
var countryDb = postgres.AddDatabase("country-app-db");
var currencyDb = postgres.AddDatabase("currency-app-db");
var customerDb = postgres.AddDatabase("customer-app-db");
var employeeDb = postgres.AddDatabase("employee-app-db");
var invoiceDb = postgres.AddDatabase("invoice-app-db");
var materialDb = postgres.AddDatabase("material-app-db");
var orderDb = postgres.AddDatabase("order-app-db");
var paymentDb = postgres.AddDatabase("payment-app-db");
var purchaseOrderDb = postgres.AddDatabase("purchaseorder-app-db");
var supplierDb = postgres.AddDatabase("supplier-app-db");


// --- Helper: Inject shared secrets into a service ---
IResourceBuilder<ProjectResource> WithSharedSecrets(IResourceBuilder<ProjectResource> project)
{
    return project
        .WithEnvironment("Jwt__PublicKey", jwtPublicKey)
        .WithEnvironment("Jwt__Issuer", jwtIssuer)
        .WithEnvironment("Jwt__Audience", jwtAudience)
        .WithEnvironment("CORS__AllowedOrigins", corsAllowedOrigins);
}

// --- Service Definitions ---
// Define services that are dependencies for other services first.
var customerService = WithSharedSecrets(
    builder.AddProject<Projects.Maliev_CustomerService_Api>("maliev-customerservice-api")
        .WithReference(customerDb, "CustomerDbContext")
        .WithReference(rabbitmq)
        .WithReference(redis));

var employeeService = WithSharedSecrets(
    builder.AddProject<Projects.Maliev_EmployeeService_Api>("maliev-employeeservice-api")
        .WithReference(employeeDb, "EmployeeDbContext")
        .WithReference(rabbitmq)
        .WithReference(redis));

// Note: .WithReference(customerService) enables service discovery.
// The AuthService can now find the CustomerService using the endpoint injected by Aspire.
// The configuration key for the URL will be "services:maliev-customerservice-api:http:0"
// Your service code will need to read this key from IConfiguration.
var authService = WithSharedSecrets(
    builder.AddProject<Projects.Maliev_AuthService_Api>("maliev-authservice-api")
        .WithReference(authDb, "AuthDbContext")
        .WithReference(rabbitmq)
        .WithReference(redis)
        .WithReference(customerService)  // Enables AuthService to call CustomerService
        .WithReference(employeeService)); // Enables AuthService to call EmployeeService

var careerService = WithSharedSecrets(builder.AddProject<Projects.Maliev_CareerService_Api>("maliev-careerservice-api").WithReference(careerDb, "CareerDbContext").WithReference(rabbitmq).WithReference(redis));
// var chatbotService = WithSharedSecrets(builder.AddProject<Projects.Maliev_ChatbotService_Api>("maliev-chatbotservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis));
var contactService = WithSharedSecrets(builder.AddProject<Projects.Maliev_ContactService_Api>("maliev-contactservice-api").WithReference(contactDb, "ContactDbContext").WithReference(rabbitmq).WithReference(redis));
var countryService = WithSharedSecrets(builder.AddProject<Projects.Maliev_CountryService_Api>("maliev-countryservice-api").WithReference(countryDb, "CountryDbContext").WithReference(rabbitmq).WithReference(redis));
var currencyService = WithSharedSecrets(builder.AddProject<Projects.Maliev_CurrencyService_Api>("maliev-currencyservice-api").WithReference(currencyDb, "CurrencyDbContext").WithReference(rabbitmq).WithReference(redis));
var invoiceService = WithSharedSecrets(builder.AddProject<Projects.Maliev_InvoiceService_Api>("maliev-invoiceservice-api").WithReference(invoiceDb, "InvoiceDbContext").WithReference(rabbitmq).WithReference(redis));
var materialService = WithSharedSecrets(builder.AddProject<Projects.Maliev_MaterialService_Api>("maliev-materialservice-api").WithReference(materialDb, "MaterialDbContext").WithReference(rabbitmq).WithReference(redis));
var orderService = WithSharedSecrets(builder.AddProject<Projects.Maliev_OrderService_Api>("maliev-orderservice-api").WithReference(orderDb, "OrderDbContext").WithReference(rabbitmq).WithReference(redis));
var paymentService = WithSharedSecrets(builder.AddProject<Projects.Maliev_PaymentService_Api>("maliev-paymentservice-api").WithReference(paymentDb, "PaymentDbContext").WithReference(rabbitmq).WithReference(redis));
// var pdfService = WithSharedSecrets(builder.AddProject<Projects.Maliev_PdfService_Api>("maliev-pdfservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis));
// var predictionService = WithSharedSecrets(builder.AddProject<Projects.Maliev_PredictionService_Api>("maliev-predictionservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis));
var purchaseOrderService = WithSharedSecrets(builder.AddProject<Projects.Maliev_PurchaseOrderService_Api>("maliev-purchaseorderservice-api").WithReference(purchaseOrderDb, "PurchaseOrderDbContext").WithReference(rabbitmq).WithReference(redis));
// var quotationRequestService = WithSharedSecrets(builder.AddProject<Projects.Maliev_QuotationRequestService_Api>("maliev-quotationrequestservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis));
// var quotationService = WithSharedSecrets(builder.AddProject<Projects.Maliev_QuotationService_Api>("maliev-quotationservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis));
// var receiptService = WithSharedSecrets(builder.AddProject<Projects.Maliev_ReceiptService_Api>("maliev-receiptservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis));
var supplierService = WithSharedSecrets(
    builder.AddProject<Projects.Maliev_SupplierService_Api>("maliev-supplierservice-api")
        .WithReference(supplierDb, "ServiceDbContext")
        .WithReference(rabbitmq)
        .WithReference(redis)
        .WithReference(purchaseOrderService)
        .WithReference(invoiceService)
        .WithReference(materialService));
// var uploadService = WithSharedSecrets(builder.AddProject<Projects.Maliev_UploadService_Api>("maliev-uploadservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis));

builder.Build().Run();
