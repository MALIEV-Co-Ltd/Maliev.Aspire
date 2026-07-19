using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Maliev.Aspire.Tests.Infrastructure;

/// <summary>
/// Contract-aware setup helpers for Aspire system tests.
/// </summary>
internal static class AspireTestData
{
    public static async Task<JsonElement> CreateCustomerAsync(
        AspireTestFixture fixture,
        string prefix = "test")
    {
        var client = fixture.CreateAuthenticatedClient("CustomerService");
        var suffix = Guid.NewGuid().ToString("N");

        var response = await client.PostAsJsonAsync("/customer/v1/customers", new
        {
            firstName = prefix,
            lastName = suffix[..8],
            email = $"{prefix}.{suffix}@example.com",
            mobile = "+66812345678",
            segment = "Retail",
            tier = "Bronze",
            preferredLanguage = "en",
            timezone = "UTC",
            paymentTerms = "Due on receipt"
        });

        return await ReadRequiredJsonAsync(response, HttpStatusCode.Created);
    }

    public static async Task<JsonElement> CreateEmployeeAsync(
        AspireTestFixture fixture,
        string prefix = "EMP",
        string? workEmail = null)
    {
        var client = fixture.CreateAuthenticatedClient("EmployeeService");
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var response = await client.PostAsJsonAsync("/employee/v1/hr/employees", new
        {
            employeeNumber = $"{prefix}-{suffix}",
            firstName = prefix,
            lastName = suffix,
            workEmail = workEmail ?? $"{prefix.ToLowerInvariant()}.{suffix.ToLowerInvariant()}@maliev.test",
            dateOfBirth = new DateTime(1990, 1, 1),
            startDate = DateTime.UtcNow.Date,
            employmentType = 1,
            jobTitle = "Integration Tester"
        });

        return await ReadRequiredJsonAsync(response, HttpStatusCode.Created);
    }

    public static async Task<Guid> CreateOnboardingTemplateAsync(
        AspireTestFixture fixture,
        string prefix = "onboarding")
    {
        var client = fixture.CreateAuthenticatedClient("LifecycleService");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var response = await client.PostAsJsonAsync("/lifecycle/v1/templates", new
        {
            name = $"{prefix}-{suffix}",
            description = "Aspire system test onboarding template",
            departmentId = (Guid?)null,
            userId = Guid.Empty,
            items = new[]
            {
                new
                {
                    title = "Complete HR paperwork",
                    description = "System test onboarding task",
                    category = 0,
                    defaultAssigneeRole = "HR",
                    daysDue = 1,
                    sortOrder = 1
                }
            }
        });

        var template = await ReadRequiredJsonAsync(response, HttpStatusCode.Created);
        return template.GetProperty("id").GetGuid();
    }

    public static async Task<JsonElement> CreateCorporateCustomerAsync(
        AspireTestFixture fixture,
        string prefix = "corp")
    {
        var client = fixture.CreateAuthenticatedClient("CustomerService");
        var suffix = Guid.NewGuid().ToString("N");
        var vatNumber = Random.Shared
            .NextInt64(1_000_000_000_000, 9_999_999_999_999)
            .ToString(CultureInfo.InvariantCulture);

        var companyResponse = await client.PostAsJsonAsync("/customer/v1/companies", new
        {
            name = $"{prefix} company {suffix[..8]}",
            vatNumber,
            registrationNumber = $"REG-{suffix[..10]}",
            contactEmail = $"{prefix}.company.{suffix}@example.com",
            contactPhone = "+66812345678",
            segment = "Enterprise",
            tier = "Gold"
        });
        var company = await ReadRequiredJsonAsync(companyResponse, HttpStatusCode.Created);
        var companyId = company.GetProperty("id").GetGuid();

        var customerResponse = await client.PostAsJsonAsync("/customer/v1/customers", new
        {
            firstName = prefix,
            lastName = suffix[..8],
            email = $"{prefix}.customer.{suffix}@example.com",
            mobile = "+66812345678",
            segment = "Enterprise",
            tier = "Gold",
            preferredLanguage = "en",
            timezone = "UTC",
            paymentTerms = "Due on receipt",
            companyId,
            usesCompanyBillingAddress = true
        });

        return await ReadRequiredJsonAsync(customerResponse, HttpStatusCode.Created);
    }

    public static async Task EnsureAnnualLeavePolicyAsync(AspireTestFixture fixture)
    {
        var client = fixture.CreateAuthenticatedClient("LeaveService");
        var policiesResponse = await client.GetAsync("/leave/v1/LeavePolicies");
        var policiesContent = await policiesResponse.Content.ReadAsStringAsync();
        if (!policiesResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Expected leave policies lookup to succeed but got {policiesResponse.StatusCode}: {policiesContent}");
        }

        if (HasActiveAnnualLeavePolicy(policiesContent))
        {
            return;
        }

        var response = await client.PostAsJsonSnakeCaseAsync("/leave/v1/LeavePolicies", new
        {
            LeaveType = 1,
            DefaultEntitlement = 20m,
            AccrualRate = 0m,
            MaxCarryForward = 5m,
            RequiredApprovalLevels = 1,
            MaxConsecutiveDays = 30
        });
        var content = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.BadRequest && content.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected annual leave policy creation to succeed but got {response.StatusCode}: {content}");
    }

    public static async Task<JsonElement> EnsureCountryAsync(
        AspireTestFixture fixture,
        string iso2 = "TH",
        string name = "Thailand")
    {
        var client = fixture.CreateAuthenticatedClient("CountryService");
        var existing = await client.GetAsync($"/country/v1/countries/iso2/{iso2}");
        if (existing.StatusCode == HttpStatusCode.OK)
        {
            return await ReadRequiredJsonAsync(existing, HttpStatusCode.OK);
        }

        if (existing.StatusCode != HttpStatusCode.NotFound)
        {
            var content = await existing.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Expected OK or NotFound while looking up country {iso2} but got {existing.StatusCode}: {content}");
        }

        var response = await client.PostAsJsonAsync("/country/v1/admin/countries", new
        {
            iso2,
            iso3 = iso2 + "A",
            name,
            officialName = "Kingdom of " + name,
            region = "Asia",
            subregion = "South-Eastern Asia",
            timezones = "[]",
            borders = "[]",
            callingCodes = "[]",
            topLevelDomains = "[]",
            currencies = "{}",
            languages = "{}",
            translations = "{}",
            flags = "{}"
        });

        return await ReadRequiredJsonAsync(response, HttpStatusCode.Created);
    }

    public static async Task<JsonElement> CreateChartAccountAsync(
        AspireTestFixture fixture,
        string type,
        string name)
    {
        var client = fixture.CreateAuthenticatedClient("AccountingService");
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var response = await client.PostAsJsonAsync("/accounting/v1/chart-of-accounts", new
        {
            accountNumber = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 9000 + 1000}-{suffix}",
            name = $"{name} {suffix}",
            type,
            category = "Integration",
            isActive = true
        });

        return await ReadRequiredJsonAsync(response, HttpStatusCode.Created);
    }

    public static async Task<JsonElement> CreateInvoiceAsync(
        AspireTestFixture fixture,
        Guid? customerId = null,
        string? customerName = null)
    {
        var client = fixture.CreateAuthenticatedClient("InvoiceService");
        var customer = customerId.HasValue ? default : await CreateCorporateCustomerAsync(fixture, "invoice");
        var resolvedCustomerId = customerId ?? customer.GetProperty("id").GetGuid();
        var resolvedCustomerName = customerName
            ?? (customer.ValueKind == JsonValueKind.Undefined ? "Integration Customer" : customer.GetProperty("name").GetString())
            ?? "Integration Customer";

        var response = await client.PostAsJsonAsync("/invoice/v1/invoices", new
        {
            customerId = resolvedCustomerId,
            billingIdentityType = 1,
            customerName = resolvedCustomerName,
            customerTaxId = "0999999999999",
            billingAddress = "123 Integration Test Road, Bangkok",
            currency = "THB",
            issueDate = DateTime.UtcNow,
            dueDate = DateTime.UtcNow.AddDays(30),
            lines = new[]
            {
                new
                {
                    lineNumber = 1,
                    description = "Integration service item",
                    quantity = 1m,
                    unitPrice = 1000m,
                    taxCategory = "VAT",
                    taxRate = 7m
                }
            }
        });

        return await ReadRequiredJsonAsync(response, HttpStatusCode.Created);
    }

    public static async Task<JsonElement> CreateOrderAsync(
        AspireTestFixture fixture,
        Guid customerId,
        string requirements)
    {
        var client = fixture.CreateAuthenticatedClient("OrderService");
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var response = await client.PostAsJsonAsync("/order/v1/orders", new
        {
            customerId = customerId.ToString(),
            customerType = "Customer",
            serviceCategoryId = 1,
            requirements,
            orderedQuantity = 1,
            customerPoNumber = $"PO-{suffix}"
        });

        return await ReadRequiredJsonAsync(response, HttpStatusCode.Created);
    }

    public static async Task<JsonElement> FinalizeInvoiceAsync(AspireTestFixture fixture, Guid invoiceId)
    {
        var client = fixture.CreateAuthenticatedClient("InvoiceService");
        var response = await client.PostAsJsonAsync($"/invoice/v1/invoices/{invoiceId}/finalize", new
        {
            FinalizedBy = "aspire-system-test"
        });

        return await ReadRequiredJsonAsync(response, HttpStatusCode.OK);
    }

    private static bool HasActiveAnnualLeavePolicy(string content)
    {
        using var document = JsonDocument.Parse(content);
        return document.RootElement.ValueKind == JsonValueKind.Array &&
            document.RootElement.EnumerateArray().Any(policy =>
                TryGetInt32(policy, "leave_type", out var leaveType) &&
                leaveType == 1 &&
                (!policy.TryGetProperty("is_active", out var isActive) || isActive.GetBoolean()));
    }

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = default;
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out value);
    }

    private static async Task<JsonElement> ReadRequiredJsonAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expectedStatus)
        {
            throw new InvalidOperationException(
                $"Expected {expectedStatus} but got {response.StatusCode}: {content}");
        }

        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }
}
