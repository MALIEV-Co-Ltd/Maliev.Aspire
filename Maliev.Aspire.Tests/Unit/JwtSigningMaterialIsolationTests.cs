using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Maliev.Aspire.AppHost.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Evaluated-resource tests for verifier-only JWT configuration.
/// </summary>
public sealed class JwtSigningMaterialIsolationTests
{
    /// <summary>
    /// The final resource environment must omit both private signing mechanisms while retaining verification fields.
    /// </summary>
    [Fact]
    public async Task WithoutJwtSigningMaterial_EvaluatedEnvironment_RetainsVerifierOnlyFields()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var resource = builder.AddExecutable("contact", "dotnet", ".")
            .WithEnvironment("Jwt__PrivateKey", "private")
            .WithEnvironment("Jwt__SecurityKey", "hmac")
            .WithEnvironment("Jwt__PublicKey", "public")
            .WithEnvironment("Jwt__Issuer", "issuer")
            .WithEnvironment("Jwt__Audience", "audience")
            .WithoutJwtSigningMaterial();

        var configuration = await ExecutionConfigurationBuilder
            .Create(resource.Resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
                NullLogger.Instance,
                CancellationToken.None);
        var environment = configuration.EnvironmentVariables.ToDictionary(StringComparer.Ordinal);

        Assert.DoesNotContain("Jwt__PrivateKey", environment.Keys);
        Assert.DoesNotContain("Jwt__SecurityKey", environment.Keys);
        Assert.Equal("public", environment["Jwt__PublicKey"]);
        Assert.Equal("issuer", environment["Jwt__Issuer"]);
        Assert.Equal("audience", environment["Jwt__Audience"]);
    }
}
