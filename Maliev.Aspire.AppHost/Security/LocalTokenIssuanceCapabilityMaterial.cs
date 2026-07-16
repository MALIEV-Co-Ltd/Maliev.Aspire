using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;

namespace Maliev.Aspire.AppHost.Security;

/// <summary>
/// Carries one AppHost-lifetime RSA keypair for the isolated Auth-to-IAM token-issuance capability.
/// </summary>
/// <param name="ActiveKeyId">Lowercase SHA-256 fingerprint of the public key.</param>
/// <param name="PrivateKeyPem">Auth-only PKCS#8 private key in PEM format.</param>
/// <param name="PublicKey">IAM-only Base64-encoded SubjectPublicKeyInfo.</param>
public sealed record LocalTokenIssuanceCapabilityMaterial(
    string ActiveKeyId,
    string PrivateKeyPem,
    string PublicKey)
{
    /// <summary>Creates a fresh 2048-bit RSA keypair for one local AppHost lifetime.</summary>
    /// <param name="environmentName">The AppHost environment.</param>
    /// <returns>Fresh private and public material with a content-derived key identifier.</returns>
    public static LocalTokenIssuanceCapabilityMaterial CreateForEnvironment(string environmentName)
    {
        if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Local token-issuance capability keys are allowed only in Development or Testing.");
        }

        using var rsa = RSA.Create(2048);
        var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        try
        {
            return new LocalTokenIssuanceCapabilityMaterial(
                Convert.ToHexString(SHA256.HashData(publicKeyBytes)).ToLowerInvariant(),
                rsa.ExportPkcs8PrivateKeyPem(),
                Convert.ToBase64String(publicKeyBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(publicKeyBytes);
        }
    }
}
