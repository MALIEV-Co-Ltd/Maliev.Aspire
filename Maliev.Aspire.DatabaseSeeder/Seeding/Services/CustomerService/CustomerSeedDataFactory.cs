using Maliev.CustomerService.Domain.Entities;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.CustomerService;

/// <summary>
/// Builds deterministic customer seed data for local testing.
/// </summary>
public static class CustomerSeedDataFactory
{
    private static readonly string[] FirstNames =
    [
        "Nattaphon", "Ananya", "Krit", "Sasithorn", "Thanawat",
        "Pimchanok", "Arthit", "Mayuree", "Somchai", "Kanya"
    ];

    private static readonly string[] LastNames =
    [
        "Wanasrivwilai", "Sukhum", "Tanaka", "Chen", "Williams",
        "Garcia", "Kowalski", "Nguyen", "Patel", "Larsson"
    ];

    private static readonly string[] CompanyNames =
    [
        "Bangkok Precision Parts", "Chiang Mai Robotics", "Eastern Seaboard Tooling",
        "Global Fixture Labs", "Nordic Manufacturing Studio", "Pacific Aerospace Components",
        "Siam Medical Devices", "Urban Mobility Fabrication", "Vertex Defense Systems",
        "Wanasrivwilai Engineering"
    ];

    private static readonly string[] Segments =
    [
        CustomerSegment.Retail,
        CustomerSegment.Wholesale,
        CustomerSegment.Enterprise,
        CustomerSegment.Government
    ];

    private static readonly string[] CompanyTiers =
    [
        "Classic",
        "Silver",
        "Gold"
    ];

    /// <summary>
    /// Creates local customer seed data without orders, quotes, documents, or NDAs.
    /// </summary>
    public static CustomerSeedDataSet CreateLocalTestingData()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var countryId = Guid.Parse("00000000-0000-0000-0000-000000000764");

        var companies = CreateCompanies(createdAt);
        var customers = new List<Customer>(capacity: 50);
        var addresses = new List<Address>(capacity: 75);
        var internalNotes = new List<InternalNote>(capacity: 17);

        for (var index = 0; index < 50; index++)
        {
            var customerId = CreateGuid(1000 + index);
            var company = index % 2 == 0 ? companies[index / 5 % companies.Count] : null;
            var firstName = FirstNames[index % FirstNames.Length];
            var lastName = LastNames[(index + index / FirstNames.Length) % LastNames.Length];
            var tier = CustomerTier.All[index % CustomerTier.All.Length];

            customers.Add(new Customer
            {
                Id = customerId,
                PrincipalId = CreateGuid(2000 + index),
                FirstName = firstName,
                LastName = lastName,
                Email = $"seed.customer{index + 1:00}@seed.maliev.local",
                Mobile = $"+6681{(1000000 + index):0000000}",
                Extension = company is null ? null : $"{100 + index}",
                Landline = company is null ? null : $"+662555{index:0000}",
                Segment = Segments[index % Segments.Length],
                Tier = tier,
                PreferredLanguage = index % 3 == 0 ? "th" : "en",
                Timezone = "Asia/Bangkok",
                CommunicationPreferences = "{\"email_opt_in\":true,\"sms_opt_in\":false}",
                CompanyId = company?.Id,
                UsesCompanyBillingAddress = company is not null && index % 4 == 0,
                IsPrimaryContact = company is not null && index % 10 == 0,
                CreatedAt = createdAt.AddDays(index),
                UpdatedAt = createdAt.AddDays(index)
            });

            addresses.Add(CreateAddress(customerId, AddressType.Billing, true, index, countryId, createdAt));
            if (index % 3 == 0)
            {
                addresses.Add(CreateAddress(customerId, AddressType.Shipping, true, index + 100, countryId, createdAt));
            }

            if (index % 3 == 1)
            {
                internalNotes.Add(new InternalNote
                {
                    Id = CreateGuid(4000 + index),
                    OwnerType = OwnerType.Customer,
                    OwnerId = customerId,
                    NoteText = $"Local testing note for seed customer {index + 1:00}.",
                    CreatedBy = "database-seeder",
                    CreatedAt = createdAt.AddDays(index),
                    UpdatedAt = createdAt.AddDays(index)
                });
            }
        }

        return new CustomerSeedDataSet(
            companies,
            customers,
            addresses,
            internalNotes,
            [],
            [],
            []);
    }

    private static List<Company> CreateCompanies(DateTime createdAt)
    {
        var companies = new List<Company>(capacity: CompanyNames.Length);

        for (var index = 0; index < CompanyNames.Length; index++)
        {
            companies.Add(new Company
            {
                Id = CreateGuid(3000 + index),
                Name = CompanyNames[index],
                VatNumber = $"TH-SEED-{index + 1:0000000000}",
                RegistrationNumber = $"SEED-COMPANY-{index + 1:000}",
                ContactEmail = $"company{index + 1:00}@seed.maliev.local",
                ContactPhone = $"+662100{index:0000}",
                Segment = Segments[index % Segments.Length],
                Tier = CompanyTiers[index % CompanyTiers.Length],
                CurrentYearPurchaseValue = 0,
                CurrentYearOrderCount = 0,
                TierCalculatedAt = null,
                FullNameTh = null,
                RegistrationDate = null,
                CompanyStatus = null,
                CompanyStatusNameTh = null,
                CompanyTypeCode = null,
                BusinessObjectives = null,
                IsVerifiedFromBdex = false,
                BdexVerificationDate = null,
                StockSymbol = null,
                CreatedAt = createdAt.AddDays(index),
                UpdatedAt = createdAt.AddDays(index)
            });
        }

        return companies;
    }

    private static Address CreateAddress(
        Guid customerId,
        string type,
        bool isDefault,
        int index,
        Guid countryId,
        DateTime createdAt)
    {
        return new Address
        {
            Id = CreateGuid(5000 + index),
            OwnerType = OwnerType.Customer,
            OwnerId = customerId,
            Type = type,
            IsDefault = isDefault,
            AddressLine1 = $"{100 + index} Seed Testing Road",
            AddressLine2 = index % 2 == 0 ? $"Building {index % 9 + 1}" : null,
            District = index % 2 == 0 ? "Khlong Toei" : "Mueang",
            City = index % 4 == 0 ? "Bangkok" : "Chiang Mai",
            StateProvince = index % 4 == 0 ? "Bangkok" : "Chiang Mai",
            PostalCode = index % 4 == 0 ? "10110" : "50000",
            CountryId = countryId,
            RecipientName = $"Seed Customer {index + 1:00}",
            RecipientPhone = $"+6682{(1000000 + index):0000000}",
            CreatedAt = createdAt.AddDays(index % 50),
            UpdatedAt = createdAt.AddDays(index % 50)
        };
    }

    private static Guid CreateGuid(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
