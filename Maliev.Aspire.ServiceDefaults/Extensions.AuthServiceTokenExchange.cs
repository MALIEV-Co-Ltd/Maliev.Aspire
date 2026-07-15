using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Registers the opt-in AuthService-backed service-token exchange.
/// </summary>
public static class AuthServiceTokenExchangeExtensions
{
    /// <summary>
    /// Adds the opt-in AuthService token handler to a downstream HTTP client.
    /// </summary>
    /// <param name="builder">The downstream HTTP client builder.</param>
    /// <returns>The downstream HTTP client builder.</returns>
    public static IHttpClientBuilder AddAuthServiceAuthentication(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddHttpMessageHandler<AuthServiceTokenExchangeHandler>();
    }

    /// <summary>
    /// Adds the AuthService token provider and outbound handler without changing legacy service-account clients.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="processServiceName">The canonical code-owned process identity expected in issued tokens.</param>
    /// <returns>The application builder.</returns>
    public static IHostApplicationBuilder AddAuthServiceTokenExchange(
        this IHostApplicationBuilder builder,
        string processServiceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(processServiceName) ||
            processServiceName.Length > 128 ||
            !string.Equals(processServiceName, processServiceName.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("A bounded canonical process service name is required.", nameof(processServiceName));
        }

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(ServiceProcessIdentity)))
        {
            throw new InvalidOperationException("AuthService token exchange can be registered only once per process.");
        }

        builder.Services.AddOptions<AuthServiceTokenExchangeOptions>()
            .Bind(builder.Configuration.GetSection(AuthServiceTokenExchangeOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                _ => AuthServiceTokenProvider.HasValidTrustConfiguration(builder.Configuration),
                "Jwt:PublicKey must be an RSA SPKI key of at least 2048 bits, and Jwt:Issuer and Jwt:Audience must be absolute HTTPS identifiers.")
            .ValidateOnStart();

        builder.Services.AddSingleton(new ServiceProcessIdentity(processServiceName));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IAuthServiceTokenProvider, AuthServiceTokenProvider>();
        builder.Services.AddTransient<AuthServiceTokenExchangeHandler>();

        builder.Services.AddHttpClient(AuthServiceTokenProvider.HttpClientName, client =>
        {
            var explicitUrl = builder.Configuration["Services:AuthService:BaseUrl"];
            client.BaseAddress = !string.IsNullOrWhiteSpace(explicitUrl)
                ? new Uri(explicitUrl)
                : new Uri("https+http://AuthService");
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddServiceDiscovery();

        return builder;
    }
}
