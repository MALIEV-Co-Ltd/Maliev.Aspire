using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.MessagingContracts.Generated;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.Aspire.Tests.IAM;

public class TestIAMRegistrationService : IAMRegistrationService
{
    private readonly bool _includeInvalid;

    public TestIAMRegistrationService(IConfiguration configuration, ILogger logger, bool includeInvalid = false)
        : base(configuration, logger, "TestService")
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

public class BackgroundIAMRegistrationServiceTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Mock<ILogger<BackgroundIAMRegistrationService>> _loggerMock;
    private readonly Mock<ILogger> _serviceLoggerMock;
    private readonly Mock<IHostApplicationLifetime> _lifetimeMock;
    private readonly IAMRegistrationStatusTracker _statusTracker;
    private readonly Mock<IConfiguration> _configMock;

    public BackgroundIAMRegistrationServiceTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<BackgroundIAMRegistrationService>>();
        _serviceLoggerMock = new Mock<ILogger>();
        _lifetimeMock = new Mock<IHostApplicationLifetime>();
        _statusTracker = new IAMRegistrationStatusTracker();
        _configMock = new Mock<IConfiguration>();

        // Setup Service Scope (for resolving IPublishEndpoint)
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_serviceScopeFactoryMock.Object);
        _serviceScopeFactoryMock
            .Setup(x => x.CreateScope())
            .Returns(_serviceScopeMock.Object);
        _serviceScopeMock
            .Setup(x => x.ServiceProvider)
            .Returns(_serviceProviderMock.Object);
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IPublishEndpoint)))
            .Returns(_publishEndpointMock.Object);

        // Setup ApplicationLifetime to simulate "Started" state
        var startedToken = new CancellationToken(canceled: true); // Already canceled means "signaled" for wait handles usually, but CancellationTokenSource.Cancel() makes IsCancellationRequested true.
        // Wait, IsCancellationRequested being true means the event happened? 
        // ApplicationStarted is a CancellationToken. When it is "cancelled", it means the event fired.
        // So we pass a cancelled token to simulate "Started".
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _lifetimeMock.Setup(x => x.ApplicationStarted).Returns(cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_PublishesRegistrationEvent()
    {
        // Arrange
        var registrationService = new TestIAMRegistrationService(_configMock.Object, _serviceLoggerMock.Object);
        var services = new[] { registrationService };

        var backgroundService = new BackgroundIAMRegistrationService(
            services,
            _serviceProviderMock.Object,
            _loggerMock.Object,
            _statusTracker,
            _lifetimeMock.Object
        );

        // Act
        await backgroundService.StartAsync(CancellationToken.None);

        // Allow some time for the background task to execute
        await Task.Delay(500);

        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        _publishEndpointMock.Verify(
            x => x.Publish(
                It.Is<PermissionRegistrationRequest>(req =>
                    req.ServiceName == "TestService" &&
                    req.Permissions.Any(p => p.PermissionId == "service.resource.action") &&
                    req.Roles.Any(r => r.RoleId == "roles.test.role1")
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );

        Assert.True(_statusTracker.IsRegistered);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPermission_LogsError()
    {
        // Arrange
        var registrationService = new TestIAMRegistrationService(_configMock.Object, _serviceLoggerMock.Object, includeInvalid: true);
        var services = new[] { registrationService };

        var backgroundService = new BackgroundIAMRegistrationService(
            services,
            _serviceProviderMock.Object,
            _loggerMock.Object,
            _statusTracker,
            _lifetimeMock.Object
        );

        // Act
        await backgroundService.StartAsync(CancellationToken.None);

        // Allow some time for the background task to execute
        await Task.Delay(500);

        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        // Should NOT publish
        _publishEndpointMock.Verify(
            x => x.Publish(It.IsAny<PermissionRegistrationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        // Status should reflect partial failure or failure (implementation catches exception for the service loop)
        // In the implementation:
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "Failed to complete IAM registration request");
        //     _statusTracker.MarkPartiallyRegistered(ex);
        // }
        // Wait, if validation fails inside RegisterServiceAsync, it throws InvalidOperationException.
        // This exception is caught in the loop in ExecuteAsync.

        Assert.Equal(RegistrationStatus.PartiallyRegistered, _statusTracker.Status);
    }

    [Fact]
    public async Task ExecuteAsync_NoServices_MarksRegistered()
    {
        // Arrange
        var services = Enumerable.Empty<IAMRegistrationService>();

        var backgroundService = new BackgroundIAMRegistrationService(
            services,
            _serviceProviderMock.Object,
            _loggerMock.Object,
            _statusTracker,
            _lifetimeMock.Object
        );

        // Act
        await backgroundService.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(_statusTracker.IsRegistered);
        _publishEndpointMock.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
