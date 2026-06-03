using System.Collections.Concurrent;
using ProductCatalog.Infrastructure.Models;

namespace ProductCatalog.Infrastructure.Repositories;

public class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<int, Product> _products = new();
    private readonly ConcurrentDictionary<string, int> _skuToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _skuLocks = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId;
    private long _createWaitCount;
    private long _duplicateSkuCount;

    public Task<Product?> GetByIdAsync(int id)
    {
        return Task.FromResult(_products.TryGetValue(id, out var product) ? Copy(product) : null);
    }

    public Task<Product> CreateAsync(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        var normalizedSku = NormalizeSku(product.Sku);
        var gate = _skuLocks.GetOrAdd(normalizedSku, _ => new SemaphoreSlim(1, 1));

        if (!gate.Wait(0))
        {
            Interlocked.Increment(ref _createWaitCount);
            gate.Wait();
        }

        try
        {
            if (_skuToId.ContainsKey(normalizedSku))
            {
                Interlocked.Increment(ref _duplicateSkuCount);
                throw new DuplicateSkuException(normalizedSku);
            }

            var created = Copy(product);
            created.Id = Interlocked.Increment(ref _nextId);
            created.Sku = normalizedSku;

            _products[created.Id] = created;
            _skuToId[normalizedSku] = created.Id;
            return Task.FromResult(Copy(created));
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<Product?> UpdateAsync(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (!_products.ContainsKey(product.Id))
        {
            return Task.FromResult<Product?>(null);
        }

        var current = _products[product.Id];
        var updated = new Product
        {
            Id = current.Id,
            Sku = current.Sku,
            Name = product.Name,
            Price = product.Price
        };

        _products[updated.Id] = updated;
        return Task.FromResult<Product?>(Copy(updated));
    }

    public long GetCreateWaitCount() => Interlocked.Read(ref _createWaitCount);

    public long GetDuplicateSkuCount() => Interlocked.Read(ref _duplicateSkuCount);

    private static string NormalizeSku(string sku)
    {
        var normalized = sku?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Sku is required.", nameof(sku));
        }

        return normalized.ToUpperInvariant();
    }

    private static Product Copy(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Price = product.Price
    };
}
