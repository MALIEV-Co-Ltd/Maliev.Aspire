using Maliev.Aspire.ServiceDefaults.Authorization;
using Xunit;

namespace Maliev.Aspire.Tests.Authorization;

public class RequirePermissionAttributeTests
{
    [Fact]
    public void Constructor_SetsPermissionAndPolicy()
    {
        // Arrange
        var permission = "invoice.invoices.create";

        // Act
        var attr = new RequirePermissionAttribute(permission);

        // Assert
        Assert.Equal(permission, attr.Permission);
        Assert.Equal($"Permission:{permission}", attr.Policy);
    }

    [Fact]
    public void Constructor_WithPermissionPrefix_SetsPolicyCorrectly()
    {
        // Arrange
        var permission = "Permission:invoice.invoices.create";

        // Act
        var attr = new RequirePermissionAttribute(permission);

        // Assert
        Assert.Equal(permission, attr.Permission);
        Assert.Equal(permission, attr.Policy);
    }

    [Fact]
    public void PreValidateModel_Toggle_UpdatesPolicy()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("test.perm");

        // Act & Assert
        Assert.False(attr.PreValidateModel);
        Assert.Equal("Permission:test.perm", attr.Policy);

        attr.PreValidateModel = true;
        Assert.True(attr.PreValidateModel);
        Assert.Equal("Permission:test.perm:validate_model", attr.Policy);

        attr.PreValidateModel = false;
        Assert.False(attr.PreValidateModel);
        Assert.Equal("Permission:test.perm", attr.Policy);
    }

    [Fact]
    public void Properties_SetAndGet_Correctly()
    {
        // Arrange
        var attr = new RequirePermissionAttribute("test.perm");

        // Act
        attr.ResourcePathTemplate = "orders/{id}";
        attr.RequireLiveCheck = true;
        attr.IsCritical = true;
        attr.AuditPurpose = "Critical action";

        // Assert
        Assert.Equal("orders/{id}", attr.ResourcePathTemplate);
        Assert.True(attr.RequireLiveCheck);
        Assert.True(attr.IsCritical);
        Assert.Equal("Critical action", attr.AuditPurpose);
    }
}