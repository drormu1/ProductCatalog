using ProductCatalog.Infrastructure.Models;
using ProductCatalog.Infrastructure.Repositories;

namespace ProductCatalog.Tests;

public class InMemoryProductRepositoryConcurrencyTests
{
    [Fact]
    public async Task CreateAsync_WithSameSkuInParallel_ShouldCreateOnlyOneProduct()
    {
        var repository = new InMemoryProductRepository();
        var successCount = 0;
        var duplicateCount = 0;

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            try
            {
                await repository.CreateAsync(new Product
                {
                    Sku = "SKU-CONC-1",
                    Name = "Product",
                    Price = 100
                });

                Interlocked.Increment(ref successCount);
            }
            catch (DuplicateSkuException)
            {
                Interlocked.Increment(ref duplicateCount);
            }
        }));

        await Task.WhenAll(tasks);

        Assert.Equal(1, successCount);
        Assert.Equal(9, duplicateCount);
    }
}
