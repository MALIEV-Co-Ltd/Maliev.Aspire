using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// HTTP message handler that adds service account JWT token to outgoing IAM requests.
/// </summary>
public class ServiceAccountAuthenticationHandler : DelegatingHandler
{
    private readonly IServiceAccountTokenProvider _tokenProvider;
    private readonly ILogger<ServiceAccountAuthenticationHandler> _logger;

    public ServiceAccountAuthenticationHandler(
        IServiceAccountTokenProvider tokenProvider,
        ILogger<ServiceAccountAuthenticationHandler> logger)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("ServiceAccountAuthenticationHandler invoked for {Method} {Uri}", request.Method, request.RequestUri);

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
            _logger.LogInformation("Generated token (first 50 chars): {Token}", token.Substring(0, Math.Min(50, token.Length)));

            request.Headers.Authorization = new AuthenticationHeaderValue("ServiceAccount", token);
            _logger.LogInformation("Authorization header set on request");

            return await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ServiceAccountAuthenticationHandler.SendAsync");
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        // Don't dispose InnerHandler - it's managed by HttpClient factory
        base.Dispose(disposing);
    }
}
