using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.MessagingContracts.Contracts.Iam;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.Aspire.ServiceDefaults.Tests.IAM;

/// <summary>
/// Test implementation of IAM registration service.
/// </summary>
public class TestIAMRegistrationService : IAMRegistrationService
{
    private readonly bool _includeInvalid;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestIAMRegistrationService"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="includeInvalid">Whether to include invalid permissions.</param>
    public TestIAMRegistrationService(IConfiguration configuration, ILogger logger, bool includeInvalid = false)
        : base(configuration, logger, "TestService")
    {
        _includeInvalid = includeInvalid;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return new[]
        {
            new RoleRegistration { RoleId = "roles.test.role1", Description = "Test role description", PermissionIds = new List<string> { "service.resource.action" } }
        };
    }
}

/// <summary>
/// Unit tests for the background IAM registration service.
/// </summary>
public class BackgroundIAMRegistrationServiceTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IBus> _busMock;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Mock<ILogger<BackgroundIAMRegistrationService>> _loggerMock;
    private readonly Mock<ILogger> _serviceLoggerMock;
    private readonly Mock<IHostApplicationLifetime> _lifetimeMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly IAMRegistrationStatusTracker _statusTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundIAMRegistrationServiceTests"/> class.
    /// </summary>
    public BackgroundIAMRegistrationServiceTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _busMock = new Mock<IBus>();
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<BackgroundIAMRegistrationService>>();
        _serviceLoggerMock = new Mock<ILogger>();
        _lifetimeMock = new Mock<IHostApplicationLifetime>();
        _configMock = new Mock<IConfiguration>();
        _statusTracker = new IAMRegistrationStatusTracker();

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
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IBus)))
            .Returns(_busMock.Object);

        // ApplicationStarted is signaled by cancelling its token.
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _lifetimeMock.Setup(x => x.ApplicationStarted).Returns(cts.Token);
    }

    /// <summary>
    /// Tests that infrastructure registration bypasses the scoped transactional outbox.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_UsesRawBusInsteadOfScopedPublishEndpoint()
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

        await WaitForRegistrationCompletionAsync();

        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        _busMock.Verify(
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
        _publishEndpointMock.Verify(
            x => x.Publish(It.IsAny<PermissionRegistrationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _serviceScopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);

        Assert.True(_statusTracker.IsRegistered);
    }

    /// <summary>
    /// Tests that invalid permissions result in an error.
    /// </summary>
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

        await WaitForRegistrationCompletionAsync();

        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        // Should NOT publish
        _busMock.Verify(
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

    /// <summary>
    /// Tests that no services results in marked as registered.
    /// </summary>
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
        await WaitForRegistrationCompletionAsync();
        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(_statusTracker.IsRegistered);
        _busMock.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task WaitForRegistrationCompletionAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));

        while (_statusTracker.Status is RegistrationStatus.Pending or RegistrationStatus.Attempting)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(timeout.Token))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException("IAM registration did not complete within the expected time.");
            }
        }
    }
}
