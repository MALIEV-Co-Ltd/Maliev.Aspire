using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;

/// <summary>
/// Contains the verifier-only configuration and exact IAM profile for the Aspire-local AuthService identity.
/// </summary>
public sealed class LocalServiceIdentitySeedOptions
{
    private static readonly Regex Sha256Pattern = new(
        "^[0-9A-F]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "AspireLocalServiceIdentity";

    /// <summary>Gets the canonical IAM workload identifier.</summary>
    public const string WorkloadId = "auth-service";

    /// <summary>Gets the AuthService client identifier.</summary>
    public const string ClientId = "service-auth-service";

    /// <summary>Gets the process identity emitted in service access tokens.</summary>
    public const string ServiceName = "AuthService";

    /// <summary>Gets the server-owned IAM profile version.</summary>
    public const int ProfileVersion = 1;

    /// <summary>Gets the exact server-owned workload role.</summary>
    public const string RoleId = "roles.workloads.auth-service.v1";

    /// <summary>Gets the workload's sole permission.</summary>
    public const string WorkloadPermission = "iam.auth.resolve-permissions";

    /// <summary>Gets the local bootstrap actor's sole permission.</summary>
    public const string ProvisionPermission = "iam.workload-principals.provision";

    /// <summary>Gets the local bootstrap actor role.</summary>
    public const string ProvisionerRoleId = "roles.aspire.workload-provisioner";

    /// <summary>Gets the stable local bootstrap actor identifier.</summary>
    public static readonly Guid ProvisionerPrincipalId = new("dc7d29c5-22e8-48e3-b317-60f242f553ad");

    /// <summary>Gets the stable idempotency operation for the local canonical workload.</summary>
    public static readonly Guid ProvisionOperationId = new("83856ea6-82d8-4384-af5c-942a15b519b8");

    /// <summary>Gets whether local identity seeding is enabled.</summary>
    public bool Enabled { get; private init; }

    /// <summary>Gets the uppercase SHA-256 verifier. Plaintext is never accepted by this type.</summary>
    public string SecretHash { get; private init; } = string.Empty;

    /// <summary>Reads and validates local-only verifier configuration.</summary>
    /// <param name="configuration">Seeder configuration.</param>
    /// <param name="environmentName">Current host environment.</param>
    /// <returns>Validated options.</returns>
    public static LocalServiceIdentitySeedOptions FromConfiguration(
        IConfiguration configuration,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!IsEnabled(configuration, environmentName))
        {
            return new LocalServiceIdentitySeedOptions();
        }

        var hash = configuration[$"{SectionName}:SecretHash"];
        if (hash is null || !Sha256Pattern.IsMatch(hash))
        {
            throw new InvalidOperationException(
                "Aspire local service identity seeding requires an uppercase SHA-256 verifier.");
        }

        return new LocalServiceIdentitySeedOptions
        {
            Enabled = true,
            SecretHash = hash
        };
    }

    /// <summary>Checks the environment-gated local seed switch without reading credential material.</summary>
    /// <param name="configuration">Seeder configuration.</param>
    /// <param name="environmentName">Current host environment.</param>
    /// <returns><see langword="true"/> only when explicitly enabled in a local environment.</returns>
    public static bool IsEnabled(IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var enabled = configuration.GetValue<bool>($"{SectionName}:Enabled");
        if (enabled && !IsLocalEnvironment(environmentName))
        {
            throw new InvalidOperationException(
                "Aspire local service identity seeding is allowed only in Development or Testing.");
        }

        return enabled;
    }

    private static bool IsLocalEnvironment(string environmentName) =>
        string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Carries one AppHost-lifetime secret and its verifier while wiring local resources.
/// </summary>
/// <param name="RawSecret">Fresh Base64Url-encoded 256-bit secret.</param>
/// <param name="SecretHash">Uppercase SHA-256 verifier.</param>
public sealed record LocalServiceIdentitySeedMaterial(string RawSecret, string SecretHash)
{
    /// <summary>Creates fresh per-start seed material using the platform cryptographic RNG.</summary>
    /// <returns>Fresh raw secret and verifier.</returns>
    public static LocalServiceIdentitySeedMaterial Create()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        try
        {
            var rawSecret = Convert.ToBase64String(randomBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            var secretHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(rawSecret)));
            return new LocalServiceIdentitySeedMaterial(rawSecret, secretHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(randomBytes);
        }
    }
}
