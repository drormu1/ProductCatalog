using ProductCatalog.Infrastructure.Models;
using ProductCatalog.Infrastructure.Repositories;

namespace ProductCatalog.Tests;

public class ProductRepositoryConcurrencyTests
{
    [Fact]
    // Proves parallel creates with same SKU result in one success only.
    public async Task CreateAsync_WithSameSkuInParallel_ShouldCreateOnlyOneProduct()
    {
        var repository = new ProductRepository();
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

    [Fact]
    // Proves duplicate SKU metric matches failed parallel create attempts.
    public async Task CreateAsync_WithSameSkuInParallel_ShouldTrackDuplicateMetric()
    {
        var repository = new ProductRepository();

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            try
            {
                await repository.CreateAsync(new Product
                {
                    Sku = "SKU-CONC-2",
                    Name = "Product",
                    Price = 100
                });
            }
            catch (DuplicateSkuException)
            {
                // Expected for all but one request.
            }
        }));

        await Task.WhenAll(tasks);

        Assert.Equal(9, repository.GetDuplicateSkuCount());
    }
}
