using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("sharedsecrets.json", optional: true);

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


// --- Service Definitions ---
// Define services that are dependencies for other services first.
var customerService = builder.AddProject<Projects.Maliev_CustomerService_Api>("maliev-customerservice-api")
    .WithReference(customerDb, "CustomerDbContext")
    .WithReference(rabbitmq)
    .WithReference(redis);

var employeeService = builder.AddProject<Projects.Maliev_EmployeeService_Api>("maliev-employeeservice-api")
    .WithReference(employeeDb, "EmployeeDbContext")
    .WithReference(rabbitmq)
    .WithReference(redis);

// Note: .WithReference(customerService) enables service discovery.
// The AuthService can now find the CustomerService using the endpoint injected by Aspire.
// The configuration key for the URL will be "services:maliev-customerservice-api:http:0"
// Your service code will need to read this key from IConfiguration.
var authService = builder.AddProject<Projects.Maliev_AuthService_Api>("maliev-authservice-api")
    .WithReference(authDb, "AuthDbContext")
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WithReference(customerService) // Enables AuthService to call CustomerService
    .WithReference(employeeService); // Enables AuthService to call EmployeeService
    
var careerService = builder.AddProject<Projects.Maliev_CareerService_Api>("maliev-careerservice-api").WithReference(careerDb, "CareerDbContext").WithReference(rabbitmq).WithReference(redis);
// var chatbotService = builder.AddProject<Projects.Maliev_ChatbotService_Api>("maliev-chatbotservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
var contactService = builder.AddProject<Projects.Maliev_ContactService_Api>("maliev-contactservice-api").WithReference(contactDb, "ContactDbContext").WithReference(rabbitmq).WithReference(redis);
var countryService = builder.AddProject<Projects.Maliev_CountryService_Api>("maliev-countryservice-api").WithReference(countryDb, "CountryDbContext").WithReference(rabbitmq).WithReference(redis);
var currencyService = builder.AddProject<Projects.Maliev_CurrencyService_Api>("maliev-currencyservice-api").WithReference(currencyDb, "CurrencyDbContext").WithReference(rabbitmq).WithReference(redis);
var invoiceService = builder.AddProject<Projects.Maliev_InvoiceService_Api>("maliev-invoiceservice-api").WithReference(invoiceDb, "InvoiceDbContext").WithReference(rabbitmq).WithReference(redis);
var materialService = builder.AddProject<Projects.Maliev_MaterialService_Api>("maliev-materialservice-api").WithReference(materialDb, "MaterialDbContext").WithReference(rabbitmq).WithReference(redis);
var orderService = builder.AddProject<Projects.Maliev_OrderService_Api>("maliev-orderservice-api").WithReference(orderDb, "OrderDbContext").WithReference(rabbitmq).WithReference(redis);
var paymentService = builder.AddProject<Projects.Maliev_PaymentService_Api>("maliev-paymentservice-api").WithReference(paymentDb, "PaymentDbContext").WithReference(rabbitmq).WithReference(redis);
// var pdfService = builder.AddProject<Projects.Maliev_PdfService_Api>("maliev-pdfservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// var predictionService = builder.AddProject<Projects.Maliev_PredictionService_Api>("maliev-predictionservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
var purchaseOrderService = builder.AddProject<Projects.Maliev_PurchaseOrderService_Api>("maliev-purchaseorderservice-api").WithReference(purchaseOrderDb, "PurchaseOrderDbContext").WithReference(rabbitmq).WithReference(redis);
// var quotationRequestService = builder.AddProject<Projects.Maliev_QuotationRequestService_Api>("maliev-quotationrequestservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// var quotationService = builder.AddProject<Projects.Maliev_QuotationService_Api>("maliev-quotationservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// var receiptService = builder.AddProject<Projects.Maliev_ReceiptService_Api>("maliev-receiptservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// var supplierService = builder.AddProject<Projects.Maliev_SupplierService_Api>("maliev-supplierservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// var uploadService = builder.AddProject<Projects.Maliev_UploadService_Api>("maliev-uploadservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);

builder.Build().Run();
