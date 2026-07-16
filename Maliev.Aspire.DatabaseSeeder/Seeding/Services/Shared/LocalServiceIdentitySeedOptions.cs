using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;

/// <summary>
/// Defines one deterministic local workload identity whose authorization is owned by IAM.
/// </summary>
/// <param name="WorkloadId">Canonical workload identifier.</param>
/// <param name="ClientId">AuthService OAuth client identifier.</param>
/// <param name="ServiceName">Exact process identity emitted in access tokens.</param>
/// <param name="ProfileVersion">IAM workload profile version.</param>
/// <param name="RoleId">Canonical base role owned by the IAM profile.</param>
/// <param name="ProvisionOperationId">Stable idempotency operation for local reconciliation.</param>
public sealed record LocalServiceIdentityProfile(
    string WorkloadId,
    string ClientId,
    string ServiceName,
    int ProfileVersion,
    string RoleId,
    Guid ProvisionOperationId);

/// <summary>
/// Immutable catalog of workload identities provisioned by the local Aspire host.
/// </summary>
public static class LocalServiceIdentityProfileCatalog
{
    /// <summary>Gets the AuthService identity.</summary>
    public static LocalServiceIdentityProfile AuthService { get; } = new(
        "auth-service",
        "service-auth-service",
        "AuthService",
        1,
        "roles.workloads.auth-service.v1",
        new Guid("83856ea6-82d8-4384-af5c-942a15b519b8"));

    /// <summary>Gets the ContactService identity.</summary>
    public static LocalServiceIdentityProfile ContactService { get; } = new(
        "contact-service",
        "service-contact-service",
        "ContactService",
        1,
        "roles.workloads.contact-service.v1",
        new Guid("7f7a4a0d-15fe-4f2a-87c7-f0d3dfcb5f83"));

    /// <summary>Gets the SearchService identity.</summary>
    public static LocalServiceIdentityProfile SearchService { get; } = new(
        "search-service",
        "service-search-service",
        "SearchService",
        1,
        "roles.workloads.search-service.v1",
        new Guid("14189701-754f-46b8-a245-e70c2dfb5320"));

    /// <summary>Gets all profiles in deterministic provisioning order.</summary>
    public static IReadOnlyList<LocalServiceIdentityProfile> All { get; } =
        Array.AsReadOnly([AuthService, ContactService, SearchService]);
}

/// <summary>
/// Contains verifier-only configuration for Aspire-local workload identities.
/// </summary>
public sealed class LocalServiceIdentitySeedOptions
{
    private static readonly Regex Sha256Pattern = new(
        "^[0-9A-F]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "AspireLocalServiceIdentity";

    /// <summary>Gets the local bootstrap actor's sole permission.</summary>
    public const string ProvisionPermission = "iam.workload-principals.provision";

    /// <summary>Gets the local bootstrap actor role.</summary>
    public const string ProvisionerRoleId = "roles.aspire.workload-provisioner";

    /// <summary>Gets the stable local bootstrap actor identifier.</summary>
    public static readonly Guid ProvisionerPrincipalId = new("dc7d29c5-22e8-48e3-b317-60f242f553ad");

    /// <summary>Gets whether local identity seeding is enabled.</summary>
    public bool Enabled { get; private init; }

    /// <summary>Gets verifier hashes keyed by canonical workload ID.</summary>
    public IReadOnlyDictionary<string, string> SecretHashes { get; private init; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>Gets the validated verifier for a profile.</summary>
    /// <param name="profile">Local workload profile.</param>
    /// <returns>Uppercase SHA-256 verifier.</returns>
    public string GetSecretHash(LocalServiceIdentityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return SecretHashes.TryGetValue(profile.WorkloadId, out var hash)
            ? hash
            : throw new InvalidOperationException(
                $"No Aspire-local verifier is configured for '{profile.WorkloadId}'.");
    }

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

        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var profile in LocalServiceIdentityProfileCatalog.All)
        {
            var hash = configuration[
                $"{SectionName}:Profiles:{profile.WorkloadId}:SecretHash"];
            if (hash is null || !Sha256Pattern.IsMatch(hash))
            {
                throw new InvalidOperationException(
                    $"Aspire local service identity '{profile.WorkloadId}' requires an uppercase SHA-256 verifier.");
            }

            hashes.Add(profile.WorkloadId, hash);
        }

        return new LocalServiceIdentitySeedOptions
        {
            Enabled = true,
            SecretHashes = new ReadOnlyDictionary<string, string>(hashes)
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

    /// <summary>Creates independent per-start material for every registered local workload.</summary>
    /// <returns>Read-only material keyed by canonical workload ID.</returns>
    public static IReadOnlyDictionary<string, LocalServiceIdentitySeedMaterial> CreateCatalog()
    {
        var material = LocalServiceIdentityProfileCatalog.All.ToDictionary(
            profile => profile.WorkloadId,
            _ => Create(),
            StringComparer.Ordinal);
        return new ReadOnlyDictionary<string, LocalServiceIdentitySeedMaterial>(material);
    }
}
