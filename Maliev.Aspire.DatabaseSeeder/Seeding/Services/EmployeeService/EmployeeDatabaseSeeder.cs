using Maliev.EmployeeService.Domain.Entities;
using Maliev.EmployeeService.Domain.Enums;
using Maliev.EmployeeService.Domain.ValueObjects;
using Maliev.EmployeeService.Infrastructure.Data;
using Maliev.EmployeeService.Infrastructure.Security;
using Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;
    private readonly IPasswordService _passwordService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmployeeDatabaseSeeder"/> class.
    /// </summary>
    /// <param name="context">The employee database context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="passwordService">Password hashing service.</param>
    public EmployeeDatabaseSeeder(
        EmployeeDbContext context,
        ILogger<EmployeeDatabaseSeeder> logger,
        IConfiguration configuration,
        IPasswordService passwordService)
        : base(context, logger)
    {
        _configuration = configuration;
        _passwordService = passwordService;
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var testAdminOptions = AspireTestAdminSeedOptions.FromConfiguration(_configuration);
        if (testAdminOptions.Enabled)
        {
            await SeedAspireTestAdminAsync(testAdminOptions, cancellationToken);
            return;
        }

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

    private async Task SeedAspireTestAdminAsync(
        AspireTestAdminSeedOptions options,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Seeding Aspire local test administrator employee {Email}...", options.Email);

        var employee = await Context.Employees
            .FirstOrDefaultAsync(e => e.Id == options.EmployeeId || e.ContactInformation.WorkEmail == options.Email, cancellationToken);

        var isNew = employee == null;
        employee ??= new Employee
        {
            Id = options.EmployeeId,
            CreatedDate = DateTime.UtcNow
        };

        employee.PrincipalId = options.PrincipalId;
        employee.EmployeeNumber = options.EmployeeNumber;
        employee.LegalName = new LegalName
        {
            FirstName = options.FirstName,
            LastName = options.LastName
        };
        employee.PreferredName = options.PreferredName;
        employee.ContactInformation = new ContactInformation
        {
            WorkEmail = options.Email
        };
        employee.PasswordHash = _passwordService.HashPassword(options.Password!);
        employee.EmploymentStatus = EmploymentStatus.Active;
        employee.EmploymentType = EmploymentType.FullTime;
        employee.StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        employee.ModifiedDate = DateTime.UtcNow;

        if (isNew)
        {
            Context.Employees.Add(employee);
        }

        await Context.SaveChangesAsync(cancellationToken);

        Logger.LogInformation(
            "Successfully seeded Aspire local test administrator employee {Email} with PrincipalId {PrincipalId}.",
            options.Email,
            options.PrincipalId);
    }
}
