using System.Collections.Concurrent;
using CipherRAG.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CipherRAG.Infrastructure.Caching;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _prefixTokens = new(StringComparer.OrdinalIgnoreCase);

    public MemoryCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out object? value))
        {
            return Task.FromResult((T?)value);
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T? value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(5)
        };

        var prefix = GetPrefix(key);
        if (!string.IsNullOrEmpty(prefix))
        {
            while (true)
            {
                var cts = _prefixTokens.GetOrAdd(prefix, _ => new CancellationTokenSource());
                try
                {
                    options.AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(cts.Token));
                    break;
                }
                catch (ObjectDisposedException)
                {
                    _prefixTokens.TryRemove(prefix, out _);
                }
            }
        }

        _memoryCache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (_prefixTokens.TryRemove(prefix, out var cts))
        {
            cts.Cancel();
            // Do NOT call cts.Dispose() here immediately.
            // Calling Dispose() while concurrent threads read/bind to cts.Token throws ObjectDisposedException.
            // The .NET Garbage Collector will safely reclaim the token when all references drop.
        }
        return Task.CompletedTask;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string key, 
        Func<CancellationToken, Task<T?>> factory, 
        TimeSpan? expiration = null, 
        CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out object? rawValue))
        {
            return (T?)rawValue;
        }

        var value = await factory(cancellationToken);

        // Mitigate Cache Penetration:
        // Cache null values with a shorter TTL (e.g. 30 seconds) to avoid database hammering on non-existent IDs.
        var effectiveExpiration = value is null 
            ? TimeSpan.FromSeconds(30) 
            : (expiration ?? TimeSpan.FromMinutes(5));

        await SetAsync(key, value, effectiveExpiration, cancellationToken);
        return value;
    }

    private static string GetPrefix(string key)
    {
        var parts = key.Split(':');
        return parts.Length > 0 ? parts[0] : string.Empty;
    }
}
