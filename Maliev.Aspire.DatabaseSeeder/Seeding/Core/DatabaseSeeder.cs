using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Core;

/// <summary>
/// Non-generic interface for uniform execution of seeders.
/// </summary>
public interface IDatabaseSeeder
{
    /// <summary>
    /// Executes the database seeding operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base class for database seeders that run from Aspire AppHost.
/// </summary>
public abstract class DatabaseSeeder<TContext> : IDatabaseSeeder where TContext : DbContext
{
    /// <summary>The database context.</summary>
    protected readonly TContext Context;
    /// <summary>The logger instance.</summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseSeeder{TContext}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    protected DatabaseSeeder(TContext context, ILogger logger)
    {
        Context = context;
        Logger = logger;
    }

    /// <summary>
    /// Ensures migrations are applied and then seeds the database.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Ensuring database is ready for {Context}...", typeof(TContext).Name);
        await WaitForDatabaseAsync(cancellationToken);

        Logger.LogInformation("Applying migrations for {Context}...", typeof(TContext).Name);
        await Context.Database.MigrateAsync(cancellationToken);

        await SeedAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds the database (idempotent - safe to run multiple times).
    /// </summary>
    protected abstract Task SeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Helper to check if table has any data (for idempotency).
    /// </summary>
    protected async Task<bool> HasDataAsync<TEntity>(CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return await Context.Set<TEntity>().AnyAsync(cancellationToken);
    }

    private async Task WaitForDatabaseAsync(CancellationToken cancellationToken)
    {
        var retryCount = 0;
        while (retryCount < 120) // 2 minutes total
        {
            try
            {
                if (await Context.Database.CanConnectAsync(cancellationToken))
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Ignore connection failures during wait period
            }

            if (retryCount % 5 == 0) // Log every 5 seconds to reduce noise
            {
                Logger.LogInformation("Waiting for {Context} to become available...", typeof(TContext).Name);
            }

            await Task.Delay(1000, cancellationToken);
            retryCount++;
        }

        throw new Exception($"Timed out waiting for database connection for {typeof(TContext).Name} after 120 seconds.");
    }
}

