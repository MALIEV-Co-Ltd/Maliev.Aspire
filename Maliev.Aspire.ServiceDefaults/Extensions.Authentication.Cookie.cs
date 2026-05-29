using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Registers the shared Maliev identity cookie scheme across BFFs.
/// Both Maliev.Web.Bff and Maliev.QuoteEngine.Bff call this to participate in the same
/// .maliev.com-scoped session. The cookie is __Secure-Maliev.Identity; DataProtection keys
/// are stored in Redis (when available) under the same application name so both BFFs can
/// decrypt each other's cookies. In dev (no Redis), an ephemeral in-process key ring is used
/// and both BFFs run on the same host, so cookie sharing works naturally.
/// </summary>
public static class IdentityCookieExtensions
{
    /// <summary>Name of the shared identity cookie issued by all Maliev BFFs.</summary>
    public const string IdentityCookieName = "__Secure-Maliev.Identity";

    /// <summary>Authentication scheme name for the short-lived external OAuth temp cookie.</summary>
    public const string ExternalSchemeName = "MalievExternal";

    /// <summary>Data Protection application name shared across all Maliev BFFs for cross-app cookie decryption.</summary>
    public const string DataProtectionApplicationName = "Maliev.Identity";

    /// <summary>Redis key under which Data Protection XML keys are persisted.</summary>
    public const string DataProtectionRedisKey = "DataProtection-Keys";

    /// <summary>
    /// Adds the shared identity cookie and Data Protection key ring.
    /// Reads <c>Auth:CookieDomain</c> from config (e.g. <c>.maliev.com</c> in prod; unset in dev).
    /// Returns <see cref="AuthenticationBuilder"/> so callers can chain additional schemes (e.g. Google OAuth).
    /// </summary>
    public static AuthenticationBuilder AddMalievIdentityCookie(
        this IHostApplicationBuilder builder,
        Action<CookieAuthenticationOptions>? configurePrimary = null)
    {
        // Shared key ring — SetApplicationName ensures both BFFs derive the same protection keys.
        // Redis persistence is configured post-build via IPostConfigureOptions so IConnectionMultiplexer
        // is resolved from the already-registered singleton (from AddRedisDistributedCache or similar).
        builder.Services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName);

        builder.Services.AddSingleton<IPostConfigureOptions<KeyManagementOptions>>(sp =>
            new PostConfigureOptions<KeyManagementOptions>(Microsoft.Extensions.Options.Options.DefaultName, opts =>
            {
                var mux = sp.GetService<IConnectionMultiplexer>();
                if (mux is not null)
                {
                    opts.XmlRepository = new RedisXmlRepository(
                        () => mux.GetDatabase(),
                        DataProtectionRedisKey);
                }
            }));

        var cookieDomain = builder.Configuration["Auth:CookieDomain"];

        return builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = ExternalSchemeName;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = IdentityCookieName;
                if (!string.IsNullOrWhiteSpace(cookieDomain))
                {
                    options.Cookie.Domain = cookieDomain;
                }
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.LoginPath = "/auth/sign-in";
                options.LogoutPath = "/auth/sign-out";
                options.AccessDeniedPath = "/auth/sign-in";
                configurePrimary?.Invoke(options);
            })
            .AddCookie(ExternalSchemeName, options =>
            {
                options.Cookie.Name = "__Secure-Maliev.Identity.External";
                if (!string.IsNullOrWhiteSpace(cookieDomain))
                {
                    options.Cookie.Domain = cookieDomain;
                }
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });
    }
}
