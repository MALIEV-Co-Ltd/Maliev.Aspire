using Maliev.Aspire.AppHost.Security;
using Maliev.AuthService.Infrastructure.Security;
using Maliev.IAMService.Api.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>Proves the public-key overlap and retirement sequence used for capability rotation.</summary>
public sealed class TokenIssuanceCapabilityRotationTests
{
    /// <summary>Both keys work during overlap; only the replacement works after retirement.</summary>
    [Fact]
    public async Task OverlapRing_AcceptsOldAndNewSigners_ThenRetirementRejectsOldKey()
    {
        var targetPrincipalId = Guid.NewGuid();
        var oldMaterial = LocalTokenIssuanceCapabilityMaterial.CreateForEnvironment("Testing");
        var newMaterial = LocalTokenIssuanceCapabilityMaterial.CreateForEnvironment("Testing");
        var oldCapability = CreateCapability(oldMaterial, targetPrincipalId);
        var newCapability = CreateCapability(newMaterial, targetPrincipalId);

        Assert.True(await IsAuthorizedAsync(oldCapability, oldMaterial, newMaterial));
        Assert.True(await IsAuthorizedAsync(newCapability, oldMaterial, newMaterial));

        Assert.False(await IsAuthorizedAsync(oldCapability, newMaterial));
        Assert.True(await IsAuthorizedAsync(newCapability, newMaterial));
    }

    /// <summary>A trusted key ID cannot make a signature from another private key valid.</summary>
    [Fact]
    public async Task Ring_RejectsKnownKeyIdWithWrongSignature()
    {
        var trustedMaterial = LocalTokenIssuanceCapabilityMaterial.CreateForEnvironment("Testing");
        var untrustedMaterial = LocalTokenIssuanceCapabilityMaterial.CreateForEnvironment("Testing");
        var mismatchedMaterial = untrustedMaterial with { ActiveKeyId = trustedMaterial.ActiveKeyId };
        var capability = CreateCapability(mismatchedMaterial, Guid.NewGuid());

        Assert.False(await IsAuthorizedAsync(capability, trustedMaterial));
    }

    private static string CreateCapability(
        LocalTokenIssuanceCapabilityMaterial material,
        Guid targetPrincipalId)
    {
        var signer = new TokenIssuanceCapabilitySigner(
            Options.Create(new TokenIssuanceCapabilityOptions
            {
                ActiveKeyId = material.ActiveKeyId,
                PrivateKey = material.PrivateKeyPem,
                LifetimeSeconds = 30
            }),
            TimeProvider.System);
        return signer.CreateCapability(targetPrincipalId);
    }

    private static async Task<bool> IsAuthorizedAsync(
        string capability,
        params LocalTokenIssuanceCapabilityMaterial[] trustedMaterials)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            [$"{TokenIssuanceCapabilityAuthentication.ConfigurationSection}:Issuer"] =
                TokenIssuanceCapabilityOptions.Issuer,
            [$"{TokenIssuanceCapabilityAuthentication.ConfigurationSection}:Audience"] =
                TokenIssuanceCapabilityOptions.Audience
        };
        foreach (var material in trustedMaterials)
        {
            configurationValues[
                $"{TokenIssuanceCapabilityAuthentication.ConfigurationSection}:PublicKeys:{material.ActiveKeyId}"] =
                material.PublicKey;
        }

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns("Testing");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTokenIssuanceCapabilityAuthentication(
            new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build(),
            environment.Object);
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers.Authorization = $"Bearer {capability}";

        var authentication = await context.AuthenticateAsync(TokenIssuanceCapabilityAuthentication.Scheme);
        if (!authentication.Succeeded)
        {
            return false;
        }

        var authorization = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            authentication.Principal!,
            resource: null,
            TokenIssuanceCapabilityAuthentication.Policy);
        return authorization.Succeeded;
    }
}
