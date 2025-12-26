using System.IdentityModel.Tokens.Jwt;
using Maliev.Aspire.ServiceDefaults.Testing;
using Xunit;

namespace Maliev.Aspire.Tests.Testing;

public class IAMTestHelpersTests
{
    [Fact]
    public void CreateTestJWT_IncludesPermissions()
    {
        // Arrange
        var permissions = new[] { "invoice.read", "invoice.create" };
        var principalId = Guid.NewGuid().ToString();

        // Act
        var tokenString = IAMTestHelpers.CreateTestJWT(principalId, permissions);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        // Assert
        var permissionClaims = token.Claims.Where(c => c.Type == "permissions").Select(c => c.Value).ToList();
        Assert.Contains("invoice.read", permissionClaims);
        Assert.Contains("invoice.create", permissionClaims);
        Assert.Equal(principalId, token.Subject);
    }

    [Fact]
    public void WithTestAuth_AddsAuthorizationHeader()
    {
        // Arrange
        var client = new HttpClient();
        var permissions = new[] { "test.perm" };

        // Act
        client.WithTestAuth("test-user", permissions);

        // Assert
        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization.Scheme);
        Assert.False(string.IsNullOrEmpty(client.DefaultRequestHeaders.Authorization.Parameter));
    }
}
