using Maliev.EmployeeService.Domain.Entities;
using Maliev.EmployeeService.Domain.Enums;
using Maliev.EmployeeService.Domain.ValueObjects;
using Maliev.EmployeeService.Infrastructure.Data;
using Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.EmployeeService;

/// <summary>
/// Database seeder for employees.
/// </summary>
public class EmployeeDatabaseSeeder : DatabaseSeeder<EmployeeDbContext>
{
    private static readonly Guid BootstrapAdminPrincipalId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid BootstrapAdminEmployeeId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    /// <summary>
    /// Initializes a new instance of the <see cref="EmployeeDatabaseSeeder"/> class.
    /// </summary>
    /// <param name="context">The employee database context.</param>
    /// <param name="logger">The logger.</param>
    public EmployeeDatabaseSeeder(EmployeeDbContext context, ILogger<EmployeeDatabaseSeeder> logger)
        : base(context, logger)
    {
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await HasDataAsync<Employee>(cancellationToken))
        {
            Logger.LogInformation("Employee database already contains data. Skipping seeding.");
            return;
        }

        Logger.LogInformation("Seeding bootstrap admin employee...");

        var admin = new Employee
        {
            Id = BootstrapAdminEmployeeId,
            PrincipalId = BootstrapAdminPrincipalId,
            EmployeeNumber = "EMP-0001",
            LegalName = new LegalName
            {
                FirstName = "Bootstrap",
                LastName = "Admin"
            },
            PreferredName = "Admin",
            ContactInformation = new ContactInformation
            {
                WorkEmail = "admin@maliev.com"
            },
            EmploymentStatus = EmploymentStatus.Active,
            EmploymentType = EmploymentType.FullTime,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        Context.Employees.Add(admin);
        await Context.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Successfully seeded bootstrap admin employee.");
    }
}
