using ProductCatalog.Infrastructure.Models;
using ProductCatalog.Infrastructure.Repositories;

namespace ProductCatalog.Tests
{
    public partial class ProductServiceCachingTests
    {
        private sealed class FakeProductRepository : IProductRepository
        {
            private readonly Dictionary<int, Product> _products;

            public FakeProductRepository(params Product[] products)
            {
                _products = products.ToDictionary(x => x.Id, Clone);
            }

            public int GetByIdCalls { get; private set; }

            public Task<Product?> GetByIdAsync(int id)
            {
                GetByIdCalls++;
                return Task.FromResult(_products.TryGetValue(id, out var product) ? Clone(product) : null);
            }

            public Task<Product> CreateAsync(Product product)
            {
                var nextId = _products.Count == 0 ? 1 : _products.Keys.Max() + 1;
                var created = Clone(product);
                created.Id = nextId;
                _products[created.Id] = created;
                return Task.FromResult(Clone(created));
            }

            public Task<Product?> UpdateAsync(Product product)
            {
                if (!_products.ContainsKey(product.Id))
                {
                    return Task.FromResult<Product?>(null);
                }

                var updated = Clone(product);
                _products[updated.Id] = updated;
                return Task.FromResult<Product?>(Clone(updated));
            }

            public long GetCreateWaitCount() => 0;

            public long GetDuplicateSkuCount() => 0;

            private static Product Clone(Product product) => new()
            {
                Id = product.Id,
                Sku = product.Sku,
                Name = product.Name,
                Price = product.Price
            };
        }
    }
}
