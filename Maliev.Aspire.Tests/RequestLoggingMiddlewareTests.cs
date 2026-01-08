using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Maliev.Aspire.ServiceDefaults.Middleware;
using System.Threading.Tasks;

namespace Maliev.Aspire.Tests;

public class RequestLoggingMiddlewareTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/liveness")]
    [InlineData("/readiness")]
    [InlineData("/metrics")]
    [InlineData("/currency/health")]
    [InlineData("/currency/liveness")]
    [InlineData("/currency/readiness")]
    [InlineData("/currency/metrics")]
    public async Task InvokeAsync_HealthCheckPaths_ShouldNotLog(string path)
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RequestLoggingMiddleware>>();
        var nextMock = new Mock<RequestDelegate>();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";

        var middleware = new RequestLoggingMiddleware(nextMock.Object, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        // Verify LogInformation was NOT called
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

        // Verify next delegate was called
        nextMock.Verify(x => x(context), Times.Once);
    }

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/orders")]
    [InlineData("/some/other/path")]
    public async Task InvokeAsync_StandardPaths_ShouldLog(string path)
    {
         // Arrange
        var loggerMock = new Mock<ILogger<RequestLoggingMiddleware>>();
        var nextMock = new Mock<RequestDelegate>();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";

        var middleware = new RequestLoggingMiddleware(nextMock.Object, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        // Verify LogInformation WAS called (at least once for start, and once for end)
         loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        // Verify next delegate was called
        nextMock.Verify(x => x(context), Times.Once);
    }
    
    [Theory]
    [InlineData("/aspire-liveness")]
    [InlineData("/currency/aspire-liveness")]
    public async Task InvokeAsync_AspireLiveness_ShouldNotLog(string path)
    {
         // Arrange
        var loggerMock = new Mock<ILogger<RequestLoggingMiddleware>>();
        var nextMock = new Mock<RequestDelegate>();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";

        var middleware = new RequestLoggingMiddleware(nextMock.Object, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        // Verified behavior: It should NOT log.
         loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "Should not log aspire-liveness requests");

        // Verify next delegate was called
        nextMock.Verify(x => x(context), Times.Once);
    }
}
