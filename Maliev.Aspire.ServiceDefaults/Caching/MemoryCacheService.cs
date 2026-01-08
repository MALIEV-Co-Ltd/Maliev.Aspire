using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace Maliev.Aspire.ServiceDefaults.Caching;

/// <summary>
/// In-memory implementation of ICacheService for development or testing when Redis is disabled.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        _cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // MemoryCache does not support pattern-based removal efficiently.
        // For local dev/fallback, we can ignore this or implement a slow scan if we tracked keys.
        // Given this is a fallback, a no-op or log warning is acceptable, 
        // but to be safe we'll just do nothing as it's hard to implement without a composite key store.
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    public Task<long> IncrementAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        // Atomic increment is tricky with generic IMemoryCache without locking.
        // This is a naive implementation for dev/test.
        if (_cache.TryGetValue(key, out object? val))
        {
            if (val is long l)
            {
                var newValue = l + 1;
                _cache.Set(key, newValue, ttl);
                return Task.FromResult(newValue);
            }
            // If it's not a long, we reset or throw. Let's reset.
        }

        _cache.Set(key, 1L, ttl);
        return Task.FromResult(1L);
    }
}
