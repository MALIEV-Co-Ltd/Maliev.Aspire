using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for adding database context with PostgreSQL to the application.
/// </summary>
public static class DatabaseExtensions
{
    public static IHostApplicationBuilder AddPostgresDbContext<TContext>(
        this IHostApplicationBuilder builder,
        string? connectionName = null,
        bool enableDynamicJson = false,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureOptions = null)
        where TContext : DbContext
    {
        var connStringName = connectionName ?? typeof(TContext).Name;
        var connectionString = builder.Configuration.GetConnectionString(connStringName)
            ?? throw new InvalidOperationException($"Database connection string '{connStringName}' not configured");

        // Enhance connection string with pooling configuration if not already present
        connectionString = EnsureConnectionPooling(connectionString);

        // Build data source (optionally with dynamic JSON)
        Npgsql.NpgsqlDataSource? dataSource = null;
        if (enableDynamicJson)
        {
            var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();
            dataSource = dataSourceBuilder.Build();
        }

        builder.Services.AddDbContext<TContext>((sp, options) =>
        {
            if (dataSource != null)
            {
                options.UseNpgsql(dataSource, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);

                    // Increased from 30s to 120s to handle heavy IAM startup load
                    npgsqlOptions.CommandTimeout(120);
                });
            }
            else
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);

                    // Increased from 30s to 120s to handle heavy IAM startup load
                    npgsqlOptions.CommandTimeout(120);
                });
            }

            // Enable detailed logging in development for debugging
            if (builder.Environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }

            // Apply custom configuration if provided
            configureOptions?.Invoke(sp, options);
        });

        // Add DbContext to health checks
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<TContext>(
                tags: new[] { "db", "ready" });

        return builder;
    }

    /// <summary>
    /// Adds a PostgreSQL DbContext with resilience and optimized configuration.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to register.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureOptions">Optional action to configure DbContext options.</param>
    /// <param name="connectionName">The name of the connection string (defaults to TContext name).</param>
    /// <param name="enableDynamicJson">Whether to enable dynamic JSON support for storing polymorphic types.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddPostgresDbContext<TContext>(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder>? configureOptions,
        string? connectionName = null,
        bool enableDynamicJson = false)
        where TContext : DbContext
    {
        return builder.AddPostgresDbContext<TContext>(
            connectionName,
            enableDynamicJson,
            (sp, options) => configureOptions?.Invoke(options));
    }

    /// <summary>
    /// Applies database migrations on application startup.
    /// Should be called after building the WebApplication.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to migrate.</typeparam>
    /// <param name="app">The web application.</param>
    /// <param name="maxRetries">Maximum number of connection retry attempts (default: 20).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task MigrateDatabaseAsync<TContext>(
        this IHost app,
        int? maxRetries = null)
        where TContext : DbContext
    {
        // Use fewer retries in test environment for faster feedback
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isTest = environment == "Test" || environment == "Testing";
        const int defaultRetries = 50; // Increased to 50 for all environments to handle 20+ services startup
        var retries = maxRetries ?? defaultRetries;

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

        try
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                // Wait for database connectivity
                int retryCount = 0;
                while (!await dbContext.Database.CanConnectAsync())
                {
                    if (retryCount >= retries)
                    {
                        throw new InvalidOperationException(
                            $"Database connectivity check failed after {retries} attempts");
                    }

                    retryCount++;
                    logger.LogInformation("Waiting for database connectivity (attempt {Attempt}/{Max})",
                        retryCount, retries);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                logger.LogInformation("Applying database migrations");

                // Ensure migrations history table exists before EF Core checks for it.
                // This prevents the 'fail: Microsoft.EntityFrameworkCore.Database.Command' log on the first run.
                await EnsureMigrationsHistoryTableExistsAsync(dbContext, CancellationToken.None);

                await dbContext.Database.MigrateAsync(CancellationToken.None);
                logger.LogInformation("Database migrations applied successfully");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }
    }

    /// <summary>
    /// Manually creates the migrations history table if it doesn't exist.
    /// This avoids the 'fail' log generated by EF Core's internal check.
    /// </summary>
    private static async Task EnsureMigrationsHistoryTableExistsAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            // This is the standard EF Core history table for PostgreSQL
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (" +
                "\"MigrationId\" varchar(150) NOT NULL, " +
                "\"ProductVersion\" varchar(32) NOT NULL, " +
                "CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY (\"MigrationId\")" +
                ");";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Ensures connection string has optimal pooling configuration for high-concurrency scenarios.
    /// </summary>
    private static string EnsureConnectionPooling(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

        // Set pooling parameters if not already configured
        if (!connectionString.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase))
        {
            builder.MaxPoolSize = 200; // Increase from default ~100 for 20+ services
        }
        if (!connectionString.Contains("Minimum Pool Size", StringComparison.OrdinalIgnoreCase))
        {
            builder.MinPoolSize = 10; // Keep warm connections ready
        }
        if (!connectionString.Contains("Connection Idle Lifetime", StringComparison.OrdinalIgnoreCase))
        {
            builder.ConnectionIdleLifetime = 300; // Recycle idle connections after 5 minutes
        }
        if (!connectionString.Contains("Connection Pruning Interval", StringComparison.OrdinalIgnoreCase))
        {
            builder.ConnectionPruningInterval = 10; // Check for stale connections every 10 seconds
        }

        return builder.ConnectionString;
    }
}
