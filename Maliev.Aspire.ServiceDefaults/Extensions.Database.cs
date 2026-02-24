using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for adding database context with PostgreSQL to the application.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Adds a PostgreSQL DbContext with standardized configuration.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to register.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">The name of the connection string.</param>
    /// <param name="enableDynamicJson">Whether to enable dynamic JSON support.</param>
    /// <param name="configureOptions">Optional action to configure DbContext options.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddPostgresDbContext<TContext>(
        this IHostApplicationBuilder builder,
        string? connectionName = null,
        bool enableDynamicJson = false,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureOptions = null)
        where TContext : DbContext
    {
        var connStringName = connectionName ?? typeof(TContext).Name;
        var connectionString = builder.Configuration.GetConnectionString(connStringName);

        if (string.IsNullOrEmpty(connectionString))
        {
            // Log available connection strings for debugging (without values for security)
            var connectionStrings = builder.Configuration.GetSection("ConnectionStrings");
            var availableKeys = connectionStrings.GetChildren().Select(c => c.Key).ToList();

            var errorMessage = $"Database connection string '{connStringName}' not configured. " +
                $"Available connection strings: [{string.Join(", ", availableKeys)}]. " +
                $"Environment: {builder.Environment.EnvironmentName}. " +
                $"IMPORTANT: Use Testcontainers for tests, NOT InMemory databases.";

            using var loggerFactory = LoggerFactory.Create(lb => lb.AddConsole());
            var logger = loggerFactory.CreateLogger("DatabaseExtensions");
            logger.LogCritical("FATAL: {ErrorMessage}", errorMessage);

            throw new InvalidOperationException(errorMessage);
        }

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
    /// <param name="maxRetries">Maximum number of connection retry attempts (default: 50).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task MigrateDatabaseAsync<TContext>(
        this IHost app,
        int? maxRetries = null,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        // Enforce a 60s timeout to prevent silent hangs (e.g. database locks)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var migrationToken = linkedCts.Token;

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
                // Wait for database connectivity with jittered retry to avoid thundering herd
                int retryCount = 0;
                var random = new Random();
                while (!await dbContext.Database.CanConnectAsync(migrationToken))
                {
                    if (retryCount >= retries)
                    {
                        throw new InvalidOperationException(
                            $"Database connectivity check failed after {retries} attempts");
                    }

                    retryCount++;
                    var delaySeconds = 5 + random.Next(0, 10); // Increased jitter (5-15s) to mitigate thundering herd on 20+ services
                    logger.LogWarning("Waiting for database connectivity (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                        retryCount, retries, delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), migrationToken);
                }

                logger.LogInformation("Applying database migrations for {ContextType}", typeof(TContext).Name);

                await dbContext.Database.MigrateAsync(migrationToken);
                logger.LogInformation("Database migrations applied successfully for {ContextType}", typeof(TContext).Name);
            });
        }
        catch (OperationCanceledException)
        {
            // Log explicitly if it was our timeout
            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                logger.LogError("Database migration timed out after 60 seconds. This may indicate a database lock or connectivity issue.");
            }
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations for {ContextType}", typeof(TContext).Name);
            throw;
        }
    }

    /// <summary>
    /// Ensures connection string has optimal pooling configuration optimized for low-spec nodes.
    /// Configured for n1-standard-1 (1 vCPU, 3.75GB RAM) with max 20 connections to conserve resources.
    /// </summary>
    private static string EnsureConnectionPooling(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

        // Optimize for low-spec nodes (n1-standard-1: 1 vCPU, 3.75GB RAM)
        // Lower connection pool to prevent resource exhaustion
        if (builder.MaxPoolSize == 100)
        {
            builder.MaxPoolSize = 20; // Reduced from 200 for low-spec nodes
        }
        if (builder.MinPoolSize == 0)
        {
            builder.MinPoolSize = 2; // Minimal warm connections to save memory
        }
        if (builder.ConnectionIdleLifetime == 300)
        {
            builder.ConnectionIdleLifetime = 60; // Recycle idle connections faster (1 minute)
        }
        if (builder.ConnectionPruningInterval == 10)
        {
            builder.ConnectionPruningInterval = 10; // Check for stale connections every 10 seconds
        }

        return builder.ConnectionString;
    }
}
