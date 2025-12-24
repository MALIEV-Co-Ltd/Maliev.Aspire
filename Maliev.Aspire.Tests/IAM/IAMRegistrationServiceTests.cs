using System.Net;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Maliev.Aspire.Tests.IAM;

public class TestIAMRegistrationService : IAMRegistrationService
{
    private readonly bool _includeInvalid;

    public TestIAMRegistrationService(IHttpClientFactory httpClientFactory, ILogger logger, bool includeInvalid = false)
        : base(httpClientFactory, logger, "TestService")
    {
        _includeInvalid = includeInvalid;
    }

    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        if (_includeInvalid)
        {
            return new[] { new PermissionRegistration { PermissionId = "invalid", Description = "Invalid permission" } };
        }

        return new[]
        {
            new PermissionRegistration { PermissionId = "service.resource.action", Description = "Test permission description" }
        };
    }

    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return new[]
        {
            new RoleRegistration { RoleId = "roles.test.role1", Description = "Test role description", PermissionIds = new List<string> { "service.resource.action" } }
        };
    }
}

public class IAMRegistrationServiceTests
{
    [Fact]
    public async Task StartAsync_ValidPermissions_CallsRegistrationEndpoints()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("OK")
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient("IAMService")).Returns(httpClient);

        var loggerMock = new Mock<ILogger<TestIAMRegistrationService>>();
        var service = new TestIAMRegistrationService(httpClientFactoryMock.Object, loggerMock.Object);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/permissions/register")),
            ItExpr.IsAny<CancellationToken>()
        );

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/roles/register")),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task Integration_Registration_Succeeds_Against_WireMock()
    {
        // Arrange
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/api/v1/permissions/register").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        server
            .Given(Request.Create().WithPath("/api/v1/roles/register").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var services = new ServiceCollection();
        services.AddHttpClient("IAMService", client =>
        {
            client.BaseAddress = new Uri(server.Urls[0]);
        });
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<TestIAMRegistrationService>>();
        var clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        var registrationService = new TestIAMRegistrationService(clientFactory, logger);

        // Act
        await registrationService.StartAsync(CancellationToken.None);

        // Assert
        var permissionLog = server.LogEntries.FirstOrDefault(e => e.RequestMessage.AbsolutePath.Contains("permissions/register"));
        var roleLog = server.LogEntries.FirstOrDefault(e => e.RequestMessage.AbsolutePath.Contains("roles/register"));

        Assert.NotNull(permissionLog);
        Assert.NotNull(roleLog);
    }

    [Fact]
    public async Task StartAsync_InvalidPermissionFormat_LogsErrorAndContinues()
    {
        // Arrange
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<TestIAMRegistrationService>>();

        var service = new TestIAMRegistrationService(httpClientFactoryMock.Object, loggerMock.Object, includeInvalid: true);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to register")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
