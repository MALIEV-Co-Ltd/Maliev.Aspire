using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("sharedsecrets.json", optional: true);

var postgres = builder.AddPostgres("postgres");

var rabbitmq = builder.AddRabbitMQ("rabbitmq");

var redis = builder.AddRedis("redis");

builder.AddProject<Projects.Maliev_AuthService_Api>("maliev-authservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_CareerService_Api>("maliev-careerservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// builder.AddProject<Projects.Maliev_ChatbotService_Api>("maliev-chatbotservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_ContactService_Api>("maliev-contactservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_CountryService_Api>("maliev-countryservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_CurrencyService_Api>("maliev-currencyservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_CustomerService_Api>("maliev-customerservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_EmployeeService_Api>("maliev-employeeservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_InvoiceService_Api>("maliev-invoiceservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_MaterialService_Api>("maliev-materialservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_OrderService_Api>("maliev-orderservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
builder.AddProject<Projects.Maliev_PaymentService_Api>("maliev-paymentservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// builder.AddProject<Projects.Maliev_PdfService_Api>("maliev-pdfservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// builder.AddProject<Projects.Maliev_PredictionService_Api>("maliev-predictionservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// builder.AddProject<Projects.Maliev_PurchaseOrderService_Api>("maliev-purchaseorderservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// builder.AddProject<Projects.Maliev_QuotationRequestService_Api>("maliev-quotationrequestservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// builder.AddProject<Projects.Maliev_QuotationService_Api>("maliev-quotationservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// builder.AddProject<Projects.Maliev_ReceiptService_Api>("maliev-receiptservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// builder.AddProject<Projects.Maliev_SupplierService_Api>("maliev-supplierservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);
// builder.AddProject<Projects.Maliev_UploadService_Api>("maliev-uploadservice-api").WithReference(postgres).WithReference(rabbitmq).WithReference(redis);

builder.Build().Run();
