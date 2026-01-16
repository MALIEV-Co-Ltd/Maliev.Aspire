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

            // Use KeysAsync which uses SCAN to avoid blocking the server
            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);

                await foreach (var key in server.KeysAsync(pattern: prefixedPattern))
                {
                    await database.KeyDeleteAsync(key);
                }

                _logger.LogInformation("Finished removing keys matching pattern {Pattern} from Redis endpoint {Endpoint}", prefixedPattern, endpoint);
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

            // Use Lua script to ensure atomicity of INCR and EXPIRE
            // Only set EXPIRE if the result of INCR is 1 (new key)
            var script = @"
                local newValue = redis.call('INCR', KEYS[1])
                if newValue == 1 then
                    redis.call('EXPIRE', KEYS[1], ARGV[1])
                end
                return newValue";

            var result = await database.ScriptEvaluateAsync(
                script,
                [prefixedKey],
                [(int)ttl.TotalSeconds]);

            return (long)result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment cache key: {Key}", key);
            return 0;
        }
    }
}
