using Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using Maliev.CustomerService.Domain.Entities;
using Maliev.CustomerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.CustomerService;

/// <summary>
/// Database seeder for local customer testing data.
/// </summary>
public class CustomerDatabaseSeeder : DatabaseSeeder<CustomerDbContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerDatabaseSeeder"/> class.
    /// </summary>
    public CustomerDatabaseSeeder(CustomerDbContext context, ILogger<CustomerDatabaseSeeder> logger)
        : base(context, logger)
    {
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedData = CustomerSeedDataFactory.CreateLocalTestingData();
        var seedEmails = seedData.Customers.Select(customer => customer.Email).ToArray();
        var seedCompanyRegistrations = seedData.Companies.Select(company => company.RegistrationNumber).ToArray();

        var existingCustomerIds = await Context.Customers
            .IgnoreQueryFilters()
            .Where(customer => seedEmails.Contains(customer.Email))
            .Select(customer => customer.Id)
            .ToListAsync(cancellationToken);

        if (existingCustomerIds.Count > 0)
        {
            await RemoveExistingSeedCustomersAsync(existingCustomerIds, cancellationToken);
        }

        var existingCompanies = await Context.Companies
            .Where(company => seedCompanyRegistrations.Contains(company.RegistrationNumber))
            .ToListAsync(cancellationToken);

        if (existingCompanies.Count > 0)
        {
            await RemoveExistingSeedCompanyDataAsync(
                existingCompanies.Select(company => company.Id).ToArray(),
                cancellationToken);
        }

        Context.Companies.RemoveRange(existingCompanies);
        await Context.SaveChangesAsync(cancellationToken);

        Context.Companies.AddRange(seedData.Companies);
        Context.Customers.AddRange(seedData.Customers);
        Context.Addresses.AddRange(seedData.Addresses);
        Context.InternalNotes.AddRange(seedData.InternalNotes);

        await Context.SaveChangesAsync(cancellationToken);

        Logger.LogInformation(
            "Seeded {CustomerCount} customers, {CompanyCount} companies, {AddressCount} addresses, and {NoteCount} internal notes.",
            seedData.Customers.Count,
            seedData.Companies.Count,
            seedData.Addresses.Count,
            seedData.InternalNotes.Count);
    }

    private async Task RemoveExistingSeedCustomersAsync(
        IReadOnlyCollection<Guid> existingCustomerIds,
        CancellationToken cancellationToken)
    {
        var existingAddresses = await Context.Addresses
            .Where(address => address.OwnerType == OwnerType.Customer && existingCustomerIds.Contains(address.OwnerId))
            .ToListAsync(cancellationToken);

        var existingNotes = await Context.InternalNotes
            .Where(note => note.OwnerType == OwnerType.Customer && existingCustomerIds.Contains(note.OwnerId))
            .ToListAsync(cancellationToken);

        var existingDocuments = await Context.DocumentReferences
            .Where(document => document.OwnerType == OwnerType.Customer && existingCustomerIds.Contains(document.OwnerId))
            .ToListAsync(cancellationToken);

        var existingNdas = await Context.NDARecords
            .Where(nda => existingCustomerIds.Contains(nda.CustomerId))
            .ToListAsync(cancellationToken);

        var existingCustomers = await Context.Customers
            .IgnoreQueryFilters()
            .Where(customer => existingCustomerIds.Contains(customer.Id))
            .ToListAsync(cancellationToken);

        Context.Addresses.RemoveRange(existingAddresses);
        Context.InternalNotes.RemoveRange(existingNotes);
        Context.DocumentReferences.RemoveRange(existingDocuments);
        Context.NDARecords.RemoveRange(existingNdas);
        Context.Customers.RemoveRange(existingCustomers);

        await Context.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveExistingSeedCompanyDataAsync(
        IReadOnlyCollection<Guid> existingCompanyIds,
        CancellationToken cancellationToken)
    {
        var existingCompanyAddresses = await Context.Addresses
            .Where(address => address.OwnerType == OwnerType.Company && existingCompanyIds.Contains(address.OwnerId))
            .ToListAsync(cancellationToken);

        var existingCompanyNotes = await Context.InternalNotes
            .Where(note => note.OwnerType == OwnerType.Company && existingCompanyIds.Contains(note.OwnerId))
            .ToListAsync(cancellationToken);

        var existingCompanyDocumentReferences = await Context.DocumentReferences
            .Where(document => document.OwnerType == OwnerType.Company && existingCompanyIds.Contains(document.OwnerId))
            .ToListAsync(cancellationToken);

        var existingCompanyDocuments = await Context.CompanyDocuments
            .Where(document => existingCompanyIds.Contains(document.CompanyId))
            .ToListAsync(cancellationToken);

        Context.Addresses.RemoveRange(existingCompanyAddresses);
        Context.InternalNotes.RemoveRange(existingCompanyNotes);
        Context.DocumentReferences.RemoveRange(existingCompanyDocumentReferences);
        Context.CompanyDocuments.RemoveRange(existingCompanyDocuments);

        await Context.SaveChangesAsync(cancellationToken);
    }
}
