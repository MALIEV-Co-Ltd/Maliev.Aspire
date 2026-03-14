using System.Reflection;
using Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Maliev.Aspire.DatabaseSeeder;

public class SeederRunner
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddUserSecrets<SeederRunner>(optional: true);

        // 1. Auto-discover all Seeders
        var seederTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => !t.IsAbstract && typeof(IDatabaseSeeder).IsAssignableFrom(t))
            .ToList();

        // 2. Resolve target seeder from environment
        var seedTarget = builder.Configuration["SEED_TARGET"];
        if (string.IsNullOrEmpty(seedTarget))
        {
            throw new InvalidOperationException("SEED_TARGET environment variable is required.");
        }

        var targetType = seederTypes.FirstOrDefault(t => t.Name.Equals(seedTarget, StringComparison.OrdinalIgnoreCase));
        if (targetType == null)
        {
            throw new InvalidOperationException($"Seeder '{seedTarget}' not found in assembly.");
        }

        // 3. Determine DbContext type
        var baseType = targetType.BaseType;
        while (baseType != null && (!baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(DatabaseSeeder<>)))
        {
            baseType = baseType.BaseType;
        }

        if (baseType == null)
        {
            throw new InvalidOperationException($"Could not determine DbContext for seeder '{seedTarget}'.");
        }

        var dbContextType = baseType.GetGenericArguments()[0];

        // 4. Infer and register the DbContext
        var connectionName = InferConnectionName(builder.Configuration);
        RegisterDbContext(builder, dbContextType, connectionName);

        // Add missing dependencies for EmployeeDbContext if it's being used
        if (seedTarget.Equals("EmployeeDatabaseSeeder", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<Maliev.EmployeeService.Application.Interfaces.IEncryptionService, Maliev.EmployeeService.Infrastructure.Security.EncryptionService>();
            builder.Services.AddSingleton<Maliev.EmployeeService.Application.Interfaces.ICurrentUserService, SeederCurrentUserService>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddScoped<Maliev.EmployeeService.Infrastructure.Data.Interceptors.AuditLogInterceptor>();
            builder.Services.AddScoped<Maliev.EmployeeService.Infrastructure.Data.Interceptors.DatabaseMetricsInterceptor>();
        }

        // Add missing dependencies for CustomerDbContext if it's being used
        if (seedTarget.Equals("CustomerDatabaseSeeder", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<Maliev.CustomerService.Application.Interfaces.IEncryptionService, Maliev.CustomerService.Infrastructure.Security.EncryptionService>();
            builder.Services.AddSingleton<Maliev.CustomerService.Infrastructure.Persistence.Interceptors.EncryptionInterceptor>();
        }

        // 5. Register ONLY the target seeder to avoid instantiation errors for others
        builder.Services.AddScoped(typeof(IDatabaseSeeder), targetType);
        builder.Services.AddScoped(targetType);

        var app = builder.Build();


        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeederRunner>>();

        try
        {
            logger.LogInformation("Starting database seeding for target: {Target}", seedTarget);

            var seeders = scope.ServiceProvider.GetServices<IDatabaseSeeder>();
            logger.LogInformation("Found {Count} registered seeders: {SeederNames}",
                seeders.Count(), string.Join(", ", seeders.Select(s => s.GetType().Name)));

            var seeder = seeders.First(s => s.GetType().Name.Equals(seedTarget, StringComparison.OrdinalIgnoreCase));

            await seeder.ExecuteAsync();

            logger.LogInformation("Seeding completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seeding failed.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Mock implementation of ICurrentUserService for database seeding operations.
    /// </summary>
    private class SeederCurrentUserService : Maliev.EmployeeService.Application.Interfaces.ICurrentUserService
    {
        public Guid? PrincipalId => Guid.Parse("00000000-0000-0000-0000-000000000001"); // IAMDbContext.SystemPrincipalId
        public string? PrincipalIdentifier => "system@maliev.internal";
        public Task<Guid?> GetEmployeeIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public string? Email => "system@maliev.internal";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => true;
    }

    /// <summary>
    /// Infers the connection name from the ConnectionStrings section.
    /// </summary>
    private static string InferConnectionName(IConfiguration config)
    {
        var connStrings = config.GetSection("ConnectionStrings").GetChildren();
        var available = connStrings.Select(c => c.Key).ToList();

        if (available.Count == 1)
            return available[0]; // Only one - use it

        // Fallback: look for "*DbContext" pattern
        var candidate = available.FirstOrDefault(k => k.EndsWith("DbContext"));
        if (candidate != null)
            return candidate;

        throw new InvalidOperationException(
            $"Cannot infer connection. Available: [{string.Join(", ", available)}]. " +
            "Set CONNECTION_NAME environment variable explicitly.");
    }

    /// <summary>
    /// Registers the DbContext using the AddPostgresDbContext extension method.
    /// </summary>
    private static void RegisterDbContext(
        IHostApplicationBuilder builder,
        Type dbContextType,
        string connectionName)
    {
        // Find the specific overload we want:
        // AddPostgresDbContext<TContext>(this IHostApplicationBuilder builder, string connectionName, bool enableDynamicJson = false, Action<IServiceProvider, DbContextOptionsBuilder>? configureOptions = null)
        var method = typeof(Microsoft.Extensions.Hosting.DatabaseExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
                m.Name == "AddPostgresDbContext"
                && m.IsGenericMethod
                && m.GetParameters().Length == 4
                && m.GetParameters()[0].ParameterType == typeof(IHostApplicationBuilder)
                && m.GetParameters()[1].ParameterType == typeof(string)
                && m.GetParameters()[2].ParameterType == typeof(bool))
            ?.MakeGenericMethod(dbContextType);

        if (method == null)
            throw new InvalidOperationException("AddPostgresDbContext extension not found");

        method.Invoke(null, new object?[] { builder, connectionName, false, null });
    }
}
