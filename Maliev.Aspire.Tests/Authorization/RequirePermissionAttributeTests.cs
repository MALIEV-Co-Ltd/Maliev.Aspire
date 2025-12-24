using System.Security.Claims;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.Aspire.Tests.Authorization;

public class RequirePermissionAttributeTests
{
    private readonly Mock<ILogger<RequirePermissionAttribute>> _loggerMock = new();

    [Fact]
    public void Constructor_InvalidFormat_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new RequirePermissionAttribute("invalid-format"));
        Assert.Throws<ArgumentException>(() => new RequirePermissionAttribute("service.action"));
        Assert.Throws<ArgumentException>(() => new RequirePermissionAttribute("service.resource.action.extra"));
    }

    [Fact]
    public void Constructor_ValidFormat_Success()
    {
        // Arrange & Act
        var attr = new RequirePermissionAttribute("invoice.invoices.create");

        // Assert
        Assert.NotNull(attr);
    }

    [Fact]
    public void OnAuthorization_UserNotAuthenticated_ReturnsChallenge()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("invoice.invoices.create");
        var context = CreateAuthorizationFilterContext(isAuthenticated: false);

        // Act
        attr.OnAuthorization(context);

        // Assert
        Assert.IsType<ChallengeResult>(context.Result);
    }

    [Fact]
    public void OnAuthorization_UserMissingPermission_ReturnsForbid()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("invoice.invoices.create");
        var context = CreateAuthorizationFilterContext(isAuthenticated: true, permissions: new[] { "other.permission" });

        // Act
        attr.OnAuthorization(context);

        // Assert
        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public void OnAuthorization_UserHasPermission_Allows()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("invoice.invoices.create");
        var context = CreateAuthorizationFilterContext(isAuthenticated: true, permissions: new[] { "invoice.invoices.create" });

        // Act
        attr.OnAuthorization(context);

        // Assert
        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_UserHasWildcardPermission_Allows()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("invoice.invoices.create");
        var context = CreateAuthorizationFilterContext(isAuthenticated: true, permissions: new[] { "invoice.invoices.*" });

        // Act
        attr.OnAuthorization(context);

        // Assert
        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_UserHasGlobalWildcardPermission_Allows()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("invoice.invoices.create");
        var context = CreateAuthorizationFilterContext(isAuthenticated: true, permissions: new[] { "*" });

        // Act
        attr.OnAuthorization(context);

        // Assert
        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_UserHasCaseInsensitivePermission_Allows()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("invoice.invoices.create");
        var context = CreateAuthorizationFilterContext(isAuthenticated: true, permissions: new[] { "INVOICE.INVOICES.CREATE" });

        // Act
        attr.OnAuthorization(context);

        // Assert
        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_UserHasPartialWildcardPermission_Allows()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("invoice.invoices.create");
        var context = CreateAuthorizationFilterContext(isAuthenticated: true, permissions: new[] { "invoice.*" });

        // Act
        attr.OnAuthorization(context);

        // Assert
        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_IsCritical_LogsInformation()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("invoice.invoices.create") { IsCritical = true };
        var loggerMock = new Mock<ILogger<RequirePermissionAttribute>>();
        var services = new ServiceCollection();
        services.AddSingleton(loggerMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var context = CreateAuthorizationFilterContext(isAuthenticated: true, permissions: new[] { "invoice.invoices.create" });
        context.HttpContext.RequestServices = serviceProvider;

        // Act
        attr.OnAuthorization(context);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CRITICAL_PERMISSION_CHECK")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private AuthorizationFilterContext CreateAuthorizationFilterContext(bool isAuthenticated, string[]? permissions = null)
    {
        var claims = new List<Claim>();
        if (isAuthenticated)
        {
            if (permissions != null)
            {
                foreach (var p in permissions)
                {
                    claims.Add(new Claim("permissions", p));
                }
            }
        }

        var identity = isAuthenticated
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity();

        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }
}
