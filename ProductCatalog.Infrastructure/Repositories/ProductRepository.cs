using System.Collections.Concurrent;
using ProductCatalog.Infrastructure.Models;

namespace ProductCatalog.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<int, Product> _products = new();
    private readonly SemaphoreSlim _createGate = new(1, 1);
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

        // If the gate is not available, we wait for it.     
        if (!_createGate.Wait(0))
        {
            Interlocked.Increment(ref _createWaitCount);
            _createGate.Wait();
        }
       
        try
        {
            if (ContainsSku(normalizedSku))
            {
                Interlocked.Increment(ref _duplicateSkuCount);
                throw new DuplicateSkuException(normalizedSku);
            }

            var created = Copy(product);
            created.Id = Interlocked.Increment(ref _nextId);
            created.Sku = normalizedSku;

            _products[created.Id] = created;
            return Task.FromResult(Copy(created));
        }
        finally
        {
            // Release the gate, allowing another request to create a product.
            _createGate.Release();
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
            // SKU stays the same on update.
            Sku = current.Sku,
            Name = product.Name,
            Price = product.Price
        };

        _products[updated.Id] = updated;
        return Task.FromResult<Product?>(Copy(updated));
    }

    public long GetCreateWaitCount() => Interlocked.Read(ref _createWaitCount);

    public long GetDuplicateSkuCount() => Interlocked.Read(ref _duplicateSkuCount);

    public int[] GetProductIds()
    {
        var ids = new int[_products.Count];
        _products.Keys.CopyTo(ids, 0);
        Array.Sort(ids);
        return ids;
    }

    private static string NormalizeSku(string sku)
    {
        var normalized = sku?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Sku is required.", nameof(sku));
        }

        return normalized.ToUpperInvariant();
    }
    // Checks if the SKU already exists in the current store.
    private bool ContainsSku(string normalizedSku)
    {
        foreach (var existing in _products.Values)
        {
            if (string.Equals(existing.Sku, normalizedSku, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Product Copy(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Price = product.Price
    };
}
