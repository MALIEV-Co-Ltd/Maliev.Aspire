using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.Aspire.AppHost.Extensions;

/// <summary>
/// Platform-wide extension methods for Aspire resources.
/// </summary>
public static class MalievResourceExtensions
{
    /// <summary>
    /// Injects shared platform secrets and standard environment variables into a project resource.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithSharedSecrets(
        this IResourceBuilder<ProjectResource> project,
        SharedConfiguration config,
        IResourceBuilder<ContainerResource> grafana,
        IResourceBuilder<IResource> otelCollector)
    {
        return project
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("Jwt__PublicKey", config.JwtPublicKey)
            .WithEnvironment("Jwt__SecurityKey", config.JwtSecurityKey)
            .WithEnvironment("Jwt__PrivateKey", config.JwtPrivateKey)
            .WithEnvironment("Jwt__Issuer", config.JwtIssuer)
            .WithEnvironment("Jwt__Audience", config.JwtAudience)
            .WithEnvironment("Authentication__Google__ClientId", config.GoogleClientId)
            .WithEnvironment("Authentication__Google__ClientSecret", config.GoogleClientSecret)
            .WithEnvironment("CORS__AllowedOrigins", config.CorsAllowedOrigins)
            .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"))
            .WithEnvironment("NPGSQL_GSSAPI_AUTHENTICATION", "false")
            .WithEnvironment("PGGSSENCMODE", "disable");
    }

    /// <summary>
    /// Configures a dedicated seeder instance for the target service that can be manually triggered from the dashboard.
    /// The seeder appears as a child resource with a "Seed Database" command button.
    /// </summary>
    /// <param name="targetService">The service to seed the database for.</param>
    /// <param name="database">The database resource to seed.</param>
    /// <param name="seederName">Optional custom name for the seeder. Useful for chaining multiple seeders.</param>
    public static IResourceBuilder<ProjectResource> SeedDatabase<TSeeder>(
        this IResourceBuilder<ProjectResource> targetService,
        IResourceBuilder<PostgresDatabaseResource> database,
        string? seederName = null)
        where TSeeder : class
    {
        var seederClassName = typeof(TSeeder).Name;
        var seederProjectName = seederName ??
            $"{targetService.Resource.Name}-seeder-{seederClassName.ToLowerInvariant()}";

        var seeder = targetService.ApplicationBuilder
            .AddProject(seederProjectName,
                "../Maliev.Aspire.DatabaseSeeder/Maliev.Aspire.DatabaseSeeder.csproj")
            .WithEnvironment("SEED_TARGET", seederClassName)
            .WithReference(database)
            .WithExplicitStart()
            .WithParentRelationship(targetService);

        CopyParentEnvironment(seeder, targetService);

        seeder.WithCommand(
            name: "seed-database",
            displayName: "Seed Database",
            executeCommand: async context =>
            {
                var commandService = context.ServiceProvider.GetRequiredService<ResourceCommandService>();
                return await commandService.ExecuteCommandAsync(
                    resource: seeder.Resource,
                    commandName: "resource-start",
                    cancellationToken: context.CancellationToken);
            },
            commandOptions: new CommandOptions
            {
                IconName = "Database",
                IconVariant = IconVariant.Filled,
                IsHighlighted = true,
                Description = $"Manually seed database using {seederClassName}"
            });

        return targetService;
    }

    /// <summary>
    /// Copies environment variables from parent resource to seeder, excluding problematic variables.
    /// </summary>
    private static void CopyParentEnvironment(
        IResourceBuilder<ProjectResource> seeder,
        IResourceBuilder<ProjectResource> parent)
    {
        seeder.WithEnvironment(context =>
        {
            var parentEnvs = parent.Resource.Annotations
                .OfType<EnvironmentCallbackAnnotation>();

            foreach (var callback in parentEnvs)
            {
                callback.Callback(context);
            }

            context.EnvironmentVariables.Remove("ASPNETCORE_URLS");
            context.EnvironmentVariables.Remove("ASPNETCORE_HTTPS_PORT");
        });
    }
}
