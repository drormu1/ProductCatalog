using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductCatalog.Infrastructure.Logging;
using ProductCatalog.Infrastructure.Models;

namespace ProductCatalog.Infrastructure.Caching;

public class MemoryProductCache : IProductCache
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private readonly TimeSpan _cacheTtl;
    private readonly ILogger<MemoryProductCache> _logger;
    private long _hitCount;
    private long _missCount;
    private long _waitCount;
    private long _setCount;
    private long _refreshCount;
    private long _removeCount;
    private long _expiredCount;

    public MemoryProductCache(
        IMemoryCache cache,
        IOptions<ProductCacheSettings> settings,
        ILogger<MemoryProductCache> logger)
    {
        _cache = cache;
        _logger = logger;
        var ttlSeconds = Math.Max(1, settings.Value.TtlSeconds);
        _cacheTtl = TimeSpan.FromSeconds(ttlSeconds);
    }

    public async Task<Product?> GetOrCreateAsync(int productId, Func<Task<Product?>> factory)
    {
        var key = GetKey(productId);

        if (_cache.TryGetValue<Product>(key, out var cached))
        {
            Interlocked.Increment(ref _hitCount);
            _logger.LogInformation("{Color}[CACHE HIT]{Reset} key={Key}", LogColors.Green, LogColors.Reset, key);
            return Copy(cached!);
        }

        Interlocked.Increment(ref _missCount);
        _logger.LogInformation("{Color}[CACHE MISS]{Reset} key={Key}", LogColors.Red, LogColors.Reset, key);

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        if (gate.CurrentCount == 0)
        {
            Interlocked.Increment(ref _waitCount);
            _logger.LogInformation("{Color}[CACHE WAIT]{Reset} key={Key} waiting for gate", LogColors.Yellow, LogColors.Reset, key);
        }

        await gate.WaitAsync();

        try
        {
            _logger.LogInformation("{Color}[CACHE LOCK ACQUIRED]{Reset} key={Key}", LogColors.Cyan, LogColors.Reset, key);

            if (_cache.TryGetValue<Product>(key, out cached))
            {
                Interlocked.Increment(ref _hitCount);
                _logger.LogInformation("{Color}[CACHE HIT AFTER WAIT]{Reset} key={Key}", LogColors.Green, LogColors.Reset, key);
                return Copy(cached!);
            }

            var product = await factory();
            if (product is null)
            {
                _logger.LogInformation("{Color}[CACHE MISS-NOT FOUND]{Reset} key={Key}", LogColors.Red, LogColors.Reset, key);
                return null;
            }

            var valueToCache = Copy(product);
            _keys[key] = 0;
            Interlocked.Increment(ref _setCount);
            _cache.Set(key, valueToCache, CreateEntryOptions(key));

            _logger.LogInformation(
                "{Color}[CACHE SET]{Reset} key={Key} displayName={DisplayName} productId={ProductId} name={Name} price={Price} cacheSize={CacheSize}",
                LogColors.Green,
                LogColors.Reset,
                key,
                GetDisplayName(valueToCache),
                valueToCache.Id,
                valueToCache.Name,
                valueToCache.Price,
                _keys.Count);

            return Copy(valueToCache);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Set(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        var key = GetKey(product.Id);
        _keys[key] = 0;
        Interlocked.Increment(ref _refreshCount);
        _cache.Set(key, Copy(product), CreateEntryOptions(key));

        _logger.LogInformation(
            "{Color}[CACHE REFRESH]{Reset} key={Key} displayName={DisplayName} productId={ProductId} name={Name} price={Price} cacheSize={CacheSize}",
            LogColors.Cyan,
            LogColors.Reset,
            key,
            GetDisplayName(product),
            product.Id,
            product.Name,
            product.Price,
            _keys.Count);
    }

    public void Remove(int productId)
    {
        var key = GetKey(productId);
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        _locks.TryRemove(key, out _);
        Interlocked.Increment(ref _removeCount);

        _logger.LogInformation("{Color}[CACHE REMOVE]{Reset} key={Key}", LogColors.Yellow, LogColors.Reset, key);
    }

    public CacheStats GetStats()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(0);
        }

        return new CacheStats
        {
            HitCount = Interlocked.Read(ref _hitCount),
            MissCount = Interlocked.Read(ref _missCount),
            WaitCount = Interlocked.Read(ref _waitCount),
            SetCount = Interlocked.Read(ref _setCount),
            RefreshCount = Interlocked.Read(ref _refreshCount),
            RemoveCount = Interlocked.Read(ref _removeCount),
            ExpiredCount = Interlocked.Read(ref _expiredCount),
            CurrentSize = _keys.Count
        };
    }

    private static string GetKey(int productId) => $"product:{productId}";

    private MemoryCacheEntryOptions CreateEntryOptions(string key)
    {
        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = _cacheTtl
        };

        options.RegisterPostEvictionCallback((evictedKey, _, reason, _) =>
        {
            if (evictedKey is not string cacheKey)
            {
                return;
            }

            if (reason == EvictionReason.Replaced)
            {
                return;
            }

            if (reason == EvictionReason.Expired)
            {
                Interlocked.Increment(ref _expiredCount);
            }

            _keys.TryRemove(cacheKey, out _);
            _locks.TryRemove(cacheKey, out _);
        });

        return options;
    }

    private static string GetDisplayName(Product product) => $"{product.Name} #{product.Id}";

    private static Product Copy(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Price = product.Price
    };
}
