using System.Net.Http.Json;
using System.Text.Json;

namespace Maliev.Aspire.Tests.Infrastructure;

/// <summary>
/// Shared test utilities for async polling, retry, and assertion helpers.
/// </summary>
public static class TestHelpers
{
    private static readonly JsonSerializerOptions SnakeCaseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Posts JSON using snake_case naming for services that explicitly expose snake_case contracts.
    /// </summary>
    public static Task<HttpResponseMessage> PostAsJsonSnakeCaseAsync<T>(
        this HttpClient client,
        string requestUri,
        T value,
        CancellationToken cancellationToken = default)
    {
        return client.PostAsJsonAsync(requestUri, value, SnakeCaseJsonOptions, cancellationToken);
    }

    /// <summary>
    /// Reads JSON using snake_case naming for services that explicitly expose snake_case contracts.
    /// </summary>
    public static Task<T?> ReadFromJsonSnakeCaseAsync<T>(
        this HttpContent content,
        CancellationToken cancellationToken = default)
    {
        return content.ReadFromJsonAsync<T>(SnakeCaseJsonOptions, cancellationToken);
    }

    /// <summary>
    /// Polls an async action until the predicate returns true or timeout expires.
    /// Use this instead of hardcoded Task.Delay for eventual consistency assertions.
    /// </summary>
    /// <example>
    /// var response = await TestHelpers.WaitForAsync(
    ///     () => client.GetAsync("/api/orders/123"),
    ///     r => r.IsSuccessStatusCode,
    ///     timeout: TimeSpan.FromSeconds(30),
    ///     message: "Order was not created in time");
    /// </example>
    public static async Task<T> WaitForAsync<T>(
        Func<Task<T>> action,
        Func<T, bool> until,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        string? message = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        interval ??= TimeSpan.FromSeconds(2);
        var deadline = DateTime.UtcNow + timeout.Value;

        T result = default!;
        while (DateTime.UtcNow < deadline)
        {
            result = await action();
            if (until(result))
                return result;
            await Task.Delay(interval.Value);
        }

        throw new TimeoutException(
            message ?? $"Condition not met within {timeout.Value.TotalSeconds}s. Last result: {result}");
    }

    /// <summary>
    /// Polls an async action until it returns a successful HTTP response or timeout expires.
    /// Convenience overload for the common HTTP polling pattern.
    /// </summary>
    public static Task<HttpResponseMessage> WaitForSuccessAsync(
        Func<Task<HttpResponseMessage>> action,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        string? message = null)
    {
        return WaitForAsync(action, r => r.IsSuccessStatusCode, timeout, interval, message);
    }
}
