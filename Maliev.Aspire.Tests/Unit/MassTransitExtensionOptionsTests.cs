using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Tests MassTransit host option defaults used by Aspire system tests.
/// </summary>
public class MassTransitExtensionOptionsTests
{
    /// <summary>
    /// Verifies non-blocking test startup still gives RabbitMQ-backed buses enough time to connect.
    /// </summary>
    [Fact]
    public void AddMassTransitWithRabbitMq_TestingEnvironment_KeepsBackgroundBusStartTimeoutStable()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing"
        });

        builder.Configuration["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672";

        builder.AddMassTransitWithRabbitMq();

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<MassTransitHostOptions>>().Value;

        Assert.False(options.WaitUntilStarted);
        Assert.True(options.StartTimeout >= TimeSpan.FromSeconds(60));
    }
}
