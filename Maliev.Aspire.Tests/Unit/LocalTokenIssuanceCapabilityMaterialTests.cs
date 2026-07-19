using System.Security.Cryptography;
using Maliev.Aspire.AppHost.Security;
using Microsoft.Extensions.Hosting;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Security contract tests for the per-start Auth-to-IAM token-issuance capability keypair.
/// </summary>
public sealed class LocalTokenIssuanceCapabilityMaterialTests
{
    /// <summary>
    /// Each AppHost construction gets a fresh RSA keypair whose public fingerprint is its canonical key id.
    /// </summary>
    [Fact]
    public void Create_GeneratesFreshMatchedRsaKeypairAndFingerprintKeyId()
    {
        var first = LocalTokenIssuanceCapabilityMaterial.CreateForEnvironment(Environments.Development);
        var second = LocalTokenIssuanceCapabilityMaterial.CreateForEnvironment("Testing");

        Assert.NotEqual(first.PrivateKeyPem, second.PrivateKeyPem);
        Assert.NotEqual(first.PublicKey, second.PublicKey);
        Assert.NotEqual(first.ActiveKeyId, second.ActiveKeyId);
        Assert.Matches("^[0-9a-f]{64}$", first.ActiveKeyId);

        var publicKeyBytes = Convert.FromBase64String(first.PublicKey);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(publicKeyBytes)).ToLowerInvariant(),
            first.ActiveKeyId);

        using var privateRsa = RSA.Create();
        privateRsa.ImportFromPem(first.PrivateKeyPem);
        using var publicRsa = RSA.Create();
        publicRsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out var bytesRead);
        Assert.Equal(publicKeyBytes.Length, bytesRead);
        Assert.Equal(2048, privateRsa.KeySize);
        Assert.Equal(privateRsa.ExportSubjectPublicKeyInfo(), publicRsa.ExportSubjectPublicKeyInfo());
    }

    /// <summary>Capability key generation must fail closed outside Development and Testing.</summary>
    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void CreateForEnvironment_NonLocalEnvironment_Throws(string environmentName)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            LocalTokenIssuanceCapabilityMaterial.CreateForEnvironment(environmentName));

        Assert.Contains("Development or Testing", exception.Message, StringComparison.Ordinal);
    }
}
