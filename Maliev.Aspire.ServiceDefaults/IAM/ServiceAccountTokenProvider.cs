using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Provides service account JWT tokens for IAM registration.
/// </summary>
public interface IServiceAccountTokenProvider
{
    /// <summary>
    /// Gets the service account token for IAM registration.
    /// Auto-generates tokens on each call with short expiration for security.
    /// </summary>
    string GetToken();
}

/// <summary>
/// Auto-generates service account JWT tokens for IAM registration.
/// Tokens are generated on-demand with configurable expiration.
/// </summary>
public class ServiceAccountTokenProvider : IServiceAccountTokenProvider
{
    private readonly IConfiguration _configuration;
    private readonly string _serviceName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccountTokenProvider"/> class.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="serviceName">The name of the service.</param>
    public ServiceAccountTokenProvider(IConfiguration configuration, string serviceName)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
    }

    /// <summary>
    /// Generates a new service account token on each call.
    /// Token is valid for 1 hour by default.
    /// </summary>
    public string GetToken()
    {
        var securityKey = _configuration["Jwt:SecurityKey"]
            ?? throw new InvalidOperationException(
                "Jwt:SecurityKey not configured. Required for service account token generation.");

        // Standard issuer/audience for all Maliev services
        var issuer = _configuration["Jwt:Issuer"] ?? "https://api.maliev.com";
        var audience = _configuration["Jwt:Audience"] ?? "https://api.maliev.com";

        // Token expiration (default: 1 hour, configurable)
        var expirationMinutes = _configuration.GetValue<int?>("IAM:TokenExpirationMinutes") ?? 60;

        return GenerateToken(
            serviceName: _serviceName,
            securityKey: securityKey,
            issuer: issuer,
            audience: audience,
            expirationMinutes: expirationMinutes
        );
    }

    private static string GenerateToken(
        string serviceName,
        string securityKey,
        string issuer,
        string audience,
        int expirationMinutes)
    {
        var serviceNameLower = serviceName.ToLowerInvariant().Replace("service", "");

        var claims = new[]
        {
            new Claim("sub", $"system:service:{serviceNameLower}"),
            new Claim("service_name", serviceName),
            new Claim("user_type", "service"),
            new Claim("role", "service-account"),
            new Claim("purpose", "iam-registration"),
            new Claim("iss", issuer),
            new Claim("aud", audience),
            new Claim("permissions", "*") // Full access for service accounts in platform
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey));

        if (key.KeySize < 256)
        {
            throw new InvalidOperationException("Jwt:SecurityKey must be at least 256 bits (32 characters) for HMAC-SHA256.");
        }

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Utility for generating service account tokens (for development/testing).
/// </summary>
public static class ServiceAccountTokenGenerator
{
    /// <summary>
    /// Generates a service account JWT token for IAM registration.
    /// </summary>
    /// <param name="serviceName">Name of the service (e.g., "ChatbotService")</param>
    /// <param name="securityKey">JWT signing key</param>
    /// <param name="issuer">Token issuer</param>
    /// <param name="audience">Token audience</param>
    /// <param name="expirationYears">Token expiration in years (default: 10)</param>
    /// <returns>JWT token string</returns>
    public static string GenerateToken(
        string serviceName,
        string securityKey,
        string issuer,
        string audience,
        int expirationYears = 10)
    {
        var claims = new[]
        {
            new Claim("sub", $"system:service:{serviceName.ToLowerInvariant()}"),
            new Claim("service_name", serviceName),
            new Claim("user_type", "service"),
            new Claim("role", "service-account"),
            new Claim("purpose", "iam-registration"),
            new Claim("permissions", "*")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddYears(expirationYears),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
