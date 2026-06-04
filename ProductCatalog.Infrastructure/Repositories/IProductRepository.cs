using ProductCatalog.Infrastructure.Models;

namespace ProductCatalog.Infrastructure.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);

    Task<Product> CreateAsync(Product product);

    Task<Product?> UpdateAsync(Product product);

    long GetCreateWaitCount();

    long GetDuplicateSkuCount();

    int[] GetProductIds();
}
