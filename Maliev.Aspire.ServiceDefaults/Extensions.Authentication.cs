using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring JWT authentication in the application.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Adds JWT Bearer authentication with RSA public key validation.
    /// Reads configuration from:
    /// - Jwt:PublicKey (Base64-encoded RSA public key in PEM format)
    /// - Jwt:Issuer
    /// - Jwt:Audience
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureOptions">Optional action to configure JWT bearer options.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddJwtAuthentication(
        this IHostApplicationBuilder builder,
        Action<JwtBearerOptions>? configureOptions = null)
    {
        var publicKeyBase64 = builder.Configuration["Jwt:PublicKey"];
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];

        if (string.IsNullOrEmpty(publicKeyBase64))
        {
            // In Testing environment, the key might be configured later by the test infrastructure
            if (builder.Environment.IsEnvironment("Testing"))
            {
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        // Minimal configuration for tests - will be overridden by PostConfigureAll in tests
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            ValidateLifetime = false,
                            ValidateIssuerSigningKey = false,
                            SignatureValidator = delegate (string token, TokenValidationParameters parameters)
                            {
                                var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);
                                return jwt;
                            }
                        };

                        configureOptions?.Invoke(options);
                    });

                builder.Services.AddAuthorization();
                return builder;
            }

            throw new InvalidOperationException("JWT PublicKey not configured. Set Jwt:PublicKey in configuration.");
        }

        // Decode Base64 PEM public key
        var publicKeyPem = Encoding.UTF8.GetString(Convert.FromBase64String(publicKeyBase64));
        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrEmpty(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrEmpty(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa),
                    ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew
                };

                // Apply custom configuration if provided
                configureOptions?.Invoke(options);
            });

        builder.Services.AddAuthorization();

        return builder;
    }

    /// <summary>
    /// Adds JWT Bearer authentication with symmetric key (HMAC-SHA256) validation.
    /// Reads configuration from:
    /// - Jwt:SecurityKey (secret key)
    /// - Jwt:Issuer
    /// - Jwt:Audience
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureOptions">Optional action to configure JWT bearer options.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddJwtAuthenticationSymmetric(
        this IHostApplicationBuilder builder,
        Action<JwtBearerOptions>? configureOptions = null)
    {
        var securityKey = builder.Configuration["Jwt:SecurityKey"];
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];

        if (string.IsNullOrEmpty(securityKey))
        {
            throw new InvalidOperationException("JWT SecurityKey not configured. Set Jwt:SecurityKey in configuration.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey));

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrEmpty(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrEmpty(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                // Apply custom configuration if provided
                configureOptions?.Invoke(options);
            });

        builder.Services.AddAuthorization();

        return builder;
    }
}
