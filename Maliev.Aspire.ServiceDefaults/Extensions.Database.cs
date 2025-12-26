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

                    npgsqlOptions.CommandTimeout(30);
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

                    npgsqlOptions.CommandTimeout(30);
                });
            }

            // Suppress noisy EF Core logs during migration checks
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandError));

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
            .AddDbContextCheck<TContext>(tags: new[] { "db", "ready" });

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
        int maxRetries = 20)
        where TContext : DbContext
    {
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
                    if (retryCount >= maxRetries)
                    {
                        throw new InvalidOperationException(
                            $"Database connectivity check failed after {maxRetries} attempts");
                    }

                    retryCount++;
                    logger.LogInformation("Waiting for database connectivity (attempt {Attempt}/{Max})",
                        retryCount, maxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                logger.LogInformation("Applying database migrations");
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }
    }
}
