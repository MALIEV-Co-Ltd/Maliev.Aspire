using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// HTTP message handler that adds service account JWT token to outgoing IAM requests.
/// </summary>
public class ServiceAccountAuthenticationHandler : DelegatingHandler
{
    private readonly IServiceAccountTokenProvider _tokenProvider;
    private readonly ILogger<ServiceAccountAuthenticationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccountAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="tokenProvider">The service account token provider.</param>
    /// <param name="logger">The logger instance.</param>
    public ServiceAccountAuthenticationHandler(
        IServiceAccountTokenProvider tokenProvider,
        ILogger<ServiceAccountAuthenticationHandler> logger)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("ServiceAccountAuthenticationHandler invoked for {Method} {Uri}", request.Method, request.RequestUri);

        // Defensive check: Ensure InnerHandler is set
        if (InnerHandler == null)
        {
            var error = "InnerHandler is null - handler not properly configured in HttpClient pipeline";
            _logger.LogError(error);
            throw new InvalidOperationException(error);
        }

        try
        {
            // Get fresh token for each request
            var token = _tokenProvider.GetToken();
            _logger.LogDebug("Generated fresh service account token for request to {Uri}", request.RequestUri);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogDebug("Authorization header set on request");

            return await base.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Request to {Uri} was canceled during shutdown.", request.RequestUri);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogInformation("Connection to IAM service failed: {Message}. This is expected if the service is not yet available or in integration tests.", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ServiceAccountAuthenticationHandler.SendAsync for {Uri}", request.RequestUri);
            throw;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        // Don't dispose InnerHandler - it's managed by HttpClient factory
        base.Dispose(disposing);
    }
}
