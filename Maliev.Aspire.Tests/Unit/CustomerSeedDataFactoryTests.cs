using Maliev.Aspire.DatabaseSeeder.Seeding.Services.CustomerService;
using Maliev.CustomerService.Domain.Entities;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Tests for customer seed data generation.
/// </summary>
public class CustomerSeedDataFactoryTests
{
    /// <summary>
    /// Verifies the local customer seed dataset covers the dashboard testing scenarios without transactional data.
    /// </summary>
    [Fact]
    public void CreateLocalTestingData_DefaultDataset_ContainsRequestedCustomerVariationsOnly()
    {
        var seedData = CustomerSeedDataFactory.CreateLocalTestingData();

        Assert.Equal(50, seedData.Customers.Count);
        Assert.Equal(
            seedData.Customers.Count,
            seedData.Customers.Select(customer => $"{customer.FirstName} {customer.LastName}").Distinct().Count());
        Assert.NotEmpty(seedData.Companies);
        Assert.Contains(seedData.Customers, customer => customer.CompanyId is null);
        Assert.Contains(seedData.Customers, customer => customer.CompanyId is not null);
        Assert.True(CustomerTier.All.All(tier => seedData.Customers.Any(customer => customer.Tier == tier)));
        Assert.Contains(seedData.Customers, customer => CountAddresses(seedData.Addresses, customer.Id) == 1);
        Assert.Contains(seedData.Customers, customer => CountAddresses(seedData.Addresses, customer.Id) > 1);
        Assert.Contains(seedData.Customers, customer => seedData.InternalNotes.Any(note => note.OwnerId == customer.Id));
        Assert.Contains(seedData.Customers, customer => seedData.InternalNotes.All(note => note.OwnerId != customer.Id));
        Assert.Empty(seedData.DocumentReferences);
        Assert.Empty(seedData.CompanyDocuments);
        Assert.Empty(seedData.NdaRecords);
    }

    private static int CountAddresses(IReadOnlyCollection<Address> addresses, Guid customerId)
    {
        return addresses.Count(address => address.OwnerType == OwnerType.Customer && address.OwnerId == customerId);
    }
}
