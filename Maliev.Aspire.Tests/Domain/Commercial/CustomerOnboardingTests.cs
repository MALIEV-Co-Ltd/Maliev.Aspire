using Maliev.Aspire.Tests.Infrastructure;
using Maliev.Intranet.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Commercial;

public class CustomerOnboardingTests : MalievTestBase
{
    public CustomerOnboardingTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task OnboardCustomer_AsAdmin_Succeeds()
    {
        Output.WriteLine("=== Customer Onboarding Integration Test Starting ===");
        
        // Step 1: Ensure Country Exists
        // Using Admin Token from Base
        var country = await EnsureCountryExistsAsync("TH", "Thailand");
        Assert.NotNull(country);
        Output.WriteLine($"✓ Using Country: {country.Name} ({country.Id})");

        // Step 2: Create Customer via BFF
        var bffClient = await CreateAuthenticatedClient("maliev-intranet-bff");
        var testId = Guid.NewGuid().ToString("N")[..8];
        
        var request = new CustomerOnboardingRequest
        {
            Customer = new CreateCustomerRequest
            {
                FirstName = "Test",
                LastName = "Customer " + testId,
                Email = $"customer.{testId}@example.com",
                Segment = "Retail",
                Tier = "Bronze",
                PreferredLanguage = "en",
                Timezone = "UTC"
            },
            NewCompany = new CreateCompanyRequest
            {
                Name = "Manual Company " + testId,
                VatNumber = "TH-1234567890123",
                RegistrationNumber = "REG-" + testId,
                Segment = "Retail",
                Tier = "Bronze"
            },
            Addresses = new List<CreateAddressRequest>
            {
                new CreateAddressRequest
                {
                    Type = "Billing",
                    AddressLine1 = "123 Test St",
                    City = "Bangkok",
                    PostalCode = "10110",
                    CountryId = country.Id
                }
            }
        };

        Output.WriteLine($"\n[Step 2] Sending onboarding request via BFF...");
        var response = await bffClient.PostAsJsonAsync("/api/customers/onboard", request);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK, 
            $"Failed with {response.StatusCode}: {content}");
            
        var createdCustomer = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(createdCustomer);
        Output.WriteLine($"✓ Customer created with ID: {createdCustomer.Id}");
        
        // Step 3: Verify Addresses via Customer Service
        Output.WriteLine("\n[Step 3] Verifying addresses in Customer Service...");
        var customerServiceClient = await CreateAuthenticatedClient("maliev-customerservice-api");
        
        var addressResponse = await customerServiceClient.GetAsync($"/customer/v1/addresses?ownerType=Customer&ownerId={createdCustomer.Id}");
        addressResponse.EnsureSuccessStatusCode();
        
        var addresses = await addressResponse.Content.ReadFromJsonAsync<List<AddressSummaryDto>>();
        Assert.NotNull(addresses);
        Assert.NotEmpty(addresses);
        Assert.Equal(request.Addresses[0].AddressLine1, addresses[0].AddressLine1);
        
        Output.WriteLine("✓ Address verification successful");
    }

    private async Task<CountryDto> EnsureCountryExistsAsync(string iso2, string name)
    {
        var countryClient = await CreateAuthenticatedClient("maliev-countryservice-api");
        
        // Check if exists
        var existing = await countryClient.GetFromJsonAsync<PagedResponse<CountryDto>>($"/country/v1/countries/search?query={iso2}");
        if (existing?.Data.Any() == true) return existing.Data.First();

        // Seed if not exists
        var seedRequest = new {
            iso2 = iso2,
            iso3 = iso2 + "A", // Simple hack for test
            name = name,
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
        };
        
        var seedResponse = await countryClient.PostAsJsonAsync("/country/v1/admin/countries", seedRequest);
        seedResponse.EnsureSuccessStatusCode();
        
        return await seedResponse.Content.ReadFromJsonAsync<CountryDto>() 
               ?? throw new InvalidOperationException("Failed to seed country");
    }

    private class PagedResponse<T> { public List<T> Data { get; set; } = new(); }
    
    private class AddressSummaryDto
    {
        public string AddressLine1 { get; set; } = string.Empty;
    }
}
