using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Maliev.Aspire.ServiceDefaults.Caching;

/// <summary>
/// Standardized Redis implementation of ICacheService using IConnectionMultiplexer directly.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _instanceName;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IOptions<RedisCacheOptions> options,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
        _instanceName = options.Value.InstanceName ?? string.Empty;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var database = _redis.GetDatabase();
            var prefixedKey = _instanceName + key;
            var value = await database.StringGetAsync(prefixedKey);
            
            if (value.IsNull)
            {
                return null;
            }

            // If T is string, return directly to avoid double quotes from JSON serialization
            if (typeof(T) == typeof(string))
            {
                return value.ToString() as T;
            }

            return JsonSerializer.Deserialize<T>(value.ToString()!, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve from cache for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var database = _redis.GetDatabase();
            var prefixedKey = _instanceName + key;
            
            string json;
            if (value is string s)
            {
                json = s;
            }
            else
            {
                json = JsonSerializer.Serialize(value, _jsonOptions);
            }

            await database.StringSetAsync(prefixedKey, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _redis.GetDatabase();
            var prefixedKey = _instanceName + key;
            await database.KeyDeleteAsync(prefixedKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cache key: {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected)
        {
            _logger.LogWarning("Redis is not connected. Cannot remove by pattern: {Pattern}", pattern);
            return;
        }

        try
        {
            var endpoints = _redis.GetEndPoints();
            var database = _redis.GetDatabase();
            var prefixedPattern = _instanceName + pattern;

            // Note: Keys() can be expensive on large databases
            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                var keys = server.Keys(pattern: prefixedPattern).ToArray();
                if (keys.Length > 0)
                {
                    await database.KeyDeleteAsync(keys);
                    _logger.LogInformation("Removed {Count} keys matching pattern {Pattern} from Redis endpoint {Endpoint}", keys.Length, prefixedPattern, endpoint);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove keys by pattern {Pattern} from Redis", pattern);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _redis.GetDatabase();
            var prefixedKey = _instanceName + key;
            return await database.KeyExistsAsync(prefixedKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check key existence in Redis: {Key}", key);
            return false;
        }
    }

    public async Task<long> IncrementAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _redis.GetDatabase();
            var prefixedKey = _instanceName + key;

            // Atomically increment and set TTL if it's a new key
            var newValue = await database.StringIncrementAsync(prefixedKey);

            if (newValue == 1)
            {
                await database.KeyExpireAsync(prefixedKey, ttl);
            }

            return newValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment cache key: {Key}", key);
            return 0;
        }
    }
}
