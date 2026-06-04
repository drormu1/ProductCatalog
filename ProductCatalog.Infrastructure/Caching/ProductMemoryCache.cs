using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductCatalog.Infrastructure.Logging;
using ProductCatalog.Infrastructure.Models;

namespace ProductCatalog.Infrastructure.Caching;

public class ProductMemoryCache : IProductCache
{
    private readonly IMemoryCache _cache;
    // _locks: one gate per cache key.
    // We use SemaphoreSlim so only one request loads missing data for a key.
    // Other requests wait and then read the same cached value.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    // _keys: keeps current cache keys for monitor stats and cleanup.
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    
    private readonly TimeSpan _cacheTtl;
    private readonly ILogger<ProductMemoryCache> _logger;
    private long _hitCount;
    private long _missCount;
    private long _waitCount;
    private long _setCount;
    private long _refreshCount;
    private long _removeCount;
    private long _expiredCount;

    public ProductMemoryCache(
        IMemoryCache cache,
        IOptions<CacheSettings> settings,
        ILogger<ProductMemoryCache> logger)
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
            IncrementHit();
            LogHit(key);
            return Copy(cached!);
        }

        IncrementMiss();
        LogMiss(key);

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        // Monitoring only (does not affect locking behavior).
        if (gate.CurrentCount == 0)
        {
            IncrementWait();
            LogWait(key);
        }

        // Wait for this product key only ,if another request is already loading it, we wait here.
        await gate.WaitAsync();

        try
        {
            LogLockAcquired(key);

            // Check again after lock:
            // another request may have already set this key while we waited.
            if (_cache.TryGetValue<Product>(key, out cached))
            {
                IncrementHit();
                LogHitAfterWait(key);
                return Copy(cached!);
            }

            var product = await factory();
            if (product is null)
            {
                // Do not cache null.
                LogMissNotFound(key);
                return null;
            }

            var valueToCache = Copy(product);
            _keys[key] = 0;
            IncrementSet();
            _cache.Set(key, valueToCache, CreateEntryOptions(key));
            LogSet(key, valueToCache);

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
        IncrementRefresh();
        _cache.Set(key, Copy(product), CreateEntryOptions(key));
        LogRefresh(key, product);
    }

    public void Remove(int productId)
    {
        var key = GetKey(productId);
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        _locks.TryRemove(key, out _);
        IncrementRemove();
        LogRemove(key);
    }

    public CacheStats GetStats()
    {
        if (_cache is MemoryCache memoryCache)
        {
            // Compact(0) does not force eviction by percentage.
            // We call it here as a extra trigger before reading stats.
            memoryCache.Compact(0);
        }

        return CreateStatsSnapshot();
    }

    private static string GetKey(int productId) => $"product:{productId}";

    private MemoryCacheEntryOptions CreateEntryOptions(string key)
    {
        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = _cacheTtl            
            //AbsoluteExpirationRelativeToNow = _cacheTtl

        };

        // Register a callback to be called when the entry is evicted from the cache.
        options.RegisterPostEvictionCallback((evictedKey, _, reason, _) =>
        {
            // If the evicted key is not a string, return.
            if (evictedKey is not string cacheKey)
            {
                return;
            }

            // If the reason is replaced, this key was replaced with a newer value.
            if (reason == EvictionReason.Replaced)
            {
                // This key was replaced with a newer value.
                return;
            }

            // If the reason is expired, increment the expired count.
            if (reason == EvictionReason.Expired)
            {
                IncrementExpired();
            }
            // Remove the key from the keys dictionary and the locks dictionary.                        
            _keys.TryRemove(cacheKey, out _);
            _locks.TryRemove(cacheKey, out _);
        });

        return options;
    }

    private static string GetDisplayName(Product product) => $"{product.Name} #{product.Id}";

    private CacheStats CreateStatsSnapshot() => new()
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

    private void IncrementHit() => Interlocked.Increment(ref _hitCount);
    private void IncrementMiss() => Interlocked.Increment(ref _missCount);
    private void IncrementWait() => Interlocked.Increment(ref _waitCount);
    private void IncrementSet() => Interlocked.Increment(ref _setCount);
    private void IncrementRefresh() => Interlocked.Increment(ref _refreshCount);
    private void IncrementRemove() => Interlocked.Increment(ref _removeCount);
    private void IncrementExpired() => Interlocked.Increment(ref _expiredCount);

    private void LogHit(string key) =>
        _logger.LogInformation("{Color}[CACHE HIT]{Reset} key={Key}", LogColors.Green, LogColors.Reset, key);

    private void LogMiss(string key) =>
        _logger.LogInformation("{Color}[CACHE MISS]{Reset} key={Key}", LogColors.Red, LogColors.Reset, key);

    private void LogWait(string key) =>
        _logger.LogInformation("{Color}[CACHE WAIT]{Reset} key={Key} waiting for gate", LogColors.Yellow, LogColors.Reset, key);

    private void LogLockAcquired(string key) =>
        _logger.LogInformation("{Color}[CACHE LOCK ACQUIRED]{Reset} key={Key}", LogColors.Cyan, LogColors.Reset, key);

    private void LogHitAfterWait(string key) =>
        _logger.LogInformation("{Color}[CACHE HIT AFTER WAIT]{Reset} key={Key}", LogColors.Green, LogColors.Reset, key);

    private void LogMissNotFound(string key) =>
        _logger.LogInformation("{Color}[CACHE MISS-NOT FOUND]{Reset} key={Key}", LogColors.Red, LogColors.Reset, key);

    private void LogSet(string key, Product product) =>
        _logger.LogInformation(
            "{Color}[CACHE SET]{Reset} key={Key} displayName={DisplayName} productId={ProductId} name={Name} price={Price} cacheSize={CacheSize}",
            LogColors.Green,
            LogColors.Reset,
            key,
            GetDisplayName(product),
            product.Id,
            product.Name,
            product.Price,
            _keys.Count);

    private void LogRefresh(string key, Product product) =>
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

    private void LogRemove(string key) =>
        _logger.LogInformation("{Color}[CACHE REMOVE]{Reset} key={Key}", LogColors.Yellow, LogColors.Reset, key);

    private static Product Copy(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Price = product.Price
    };
}
