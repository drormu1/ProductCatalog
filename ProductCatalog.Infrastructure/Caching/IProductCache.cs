using ProductCatalog.Infrastructure.Models;

namespace ProductCatalog.Infrastructure.Caching;

public interface IProductCache
{
    Task<Product?> GetOrCreateAsync(int productId, Func<Task<Product?>> factory);

    void Set(Product product);

    void Remove(int productId);

    CacheStats GetStats();
}
