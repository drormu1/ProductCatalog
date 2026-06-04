using ProductCatalog.Infrastructure.Models;
using ProductCatalog.Infrastructure.Repositories;

namespace ProductCatalog.Tests
{
    public partial class ProductServiceCachingTests
    {
        private sealed class FakeProductRepository : IProductRepository
        {
            private readonly Dictionary<int, Product> _products;
            private readonly object _gate = new();
            private int _getByIdCalls;

            public FakeProductRepository(params Product[] products)
            {
                _products = products.ToDictionary(x => x.Id, Clone);
            }

            public int GetByIdCalls => Volatile.Read(ref _getByIdCalls);

            public Task<Product?> GetByIdAsync(int id)
            {
                Interlocked.Increment(ref _getByIdCalls);
                lock (_gate)
                {
                    return Task.FromResult(_products.TryGetValue(id, out var product) ? Clone(product) : null);
                }
            }

            public Task<Product> CreateAsync(Product product)
            {
                lock (_gate)
                {
                    var nextId = _products.Count == 0 ? 1 : _products.Keys.Max() + 1;
                    var created = Clone(product);
                    created.Id = nextId;
                    _products[created.Id] = created;
                    return Task.FromResult(Clone(created));
                }
            }

            public Task<Product?> UpdateAsync(Product product)
            {
                lock (_gate)
                {
                    if (!_products.ContainsKey(product.Id))
                    {
                        return Task.FromResult<Product?>(null);
                    }

                    var updated = Clone(product);
                    _products[updated.Id] = updated;
                    return Task.FromResult<Product?>(Clone(updated));
                }
            }

            public long GetCreateWaitCount() => 0;

            public long GetDuplicateSkuCount() => 0;

            public int[] GetProductIds()
            {
                lock (_gate)
                {
                    return _products.Keys.OrderBy(id => id).ToArray();
                }
            }

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
