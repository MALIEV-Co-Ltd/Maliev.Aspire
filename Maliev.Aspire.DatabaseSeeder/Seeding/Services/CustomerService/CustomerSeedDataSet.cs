using Maliev.CustomerService.Domain.Entities;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.CustomerService;

/// <summary>
/// Customer seed data grouped by entity type.
/// </summary>
public sealed class CustomerSeedDataSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerSeedDataSet"/> class.
    /// </summary>
    public CustomerSeedDataSet(
        IReadOnlyCollection<Company> companies,
        IReadOnlyCollection<Customer> customers,
        IReadOnlyCollection<Address> addresses,
        IReadOnlyCollection<InternalNote> internalNotes,
        IReadOnlyCollection<DocumentReference> documentReferences,
        IReadOnlyCollection<CompanyDocument> companyDocuments,
        IReadOnlyCollection<NDARecord> ndaRecords)
    {
        Companies = companies;
        Customers = customers;
        Addresses = addresses;
        InternalNotes = internalNotes;
        DocumentReferences = documentReferences;
        CompanyDocuments = companyDocuments;
        NdaRecords = ndaRecords;
    }

    /// <summary>
    /// Companies to seed.
    /// </summary>
    public IReadOnlyCollection<Company> Companies { get; }

    /// <summary>
    /// Customers to seed.
    /// </summary>
    public IReadOnlyCollection<Customer> Customers { get; }

    /// <summary>
    /// Addresses to seed.
    /// </summary>
    public IReadOnlyCollection<Address> Addresses { get; }

    /// <summary>
    /// Internal notes to seed.
    /// </summary>
    public IReadOnlyCollection<InternalNote> InternalNotes { get; }

    /// <summary>
    /// Document references to seed.
    /// </summary>
    public IReadOnlyCollection<DocumentReference> DocumentReferences { get; }

    /// <summary>
    /// Company documents to seed.
    /// </summary>
    public IReadOnlyCollection<CompanyDocument> CompanyDocuments { get; }

    /// <summary>
    /// NDA records to seed.
    /// </summary>
    public IReadOnlyCollection<NDARecord> NdaRecords { get; }
}
