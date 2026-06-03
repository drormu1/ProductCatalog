using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductCatalog.API.Contracts;
using ProductCatalog.API.Services;
using ProductCatalog.Infrastructure.Caching;
using ProductCatalog.Infrastructure.Models;
using ProductCatalog.Infrastructure.Repositories;
using Xunit;

namespace ProductCatalog.Tests
{
    public partial class ProductServiceCachingTests
    {
        [Fact]
        public async Task GetByIdAsync_ShouldUseCache_OnSecondRead()
        {
            var repository = new FakeProductRepository(new Product { Id = 1, Sku = "SKU-1", Name = "Keyboard", Price = 100 });
            var cache = CreateCache();
            var service = CreateService(repository, cache);

            var first = await service.GetByIdAsync(1);
            var second = await service.GetByIdAsync(1);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(1, repository.GetByIdCalls);
        }

        [Fact]
        public async Task UpdateAsync_ShouldRefreshCache_AndReturnNewestData()
        {
            var repository = new FakeProductRepository(new Product { Id = 1, Sku = "SKU-1", Name = "Keyboard", Price = 100 });
            var cache = CreateCache();
            var service = CreateService(repository, cache);

            var beforeUpdate = await service.GetByIdAsync(1);
            var updated = await service.UpdateAsync(1, new UpdateProductRequest { Name = "Keyboard", Price = 120 });
            var afterUpdate = await service.GetByIdAsync(1);

            Assert.NotNull(beforeUpdate);
            Assert.NotNull(updated);
            Assert.NotNull(afterUpdate);
            Assert.Equal(100, beforeUpdate!.Price);
            Assert.Equal(120, updated!.Price);
            Assert.Equal(120, afterUpdate!.Price);
            Assert.Equal(1, repository.GetByIdCalls);
        }

        [Fact]
        public async Task FullFlow_CreateGetGetUpdateGet_ShouldKeepCacheConsistent()
        {
            var repository = new FakeProductRepository();
            var cache = CreateCache();
            var service = CreateService(repository, cache);

            var created = await service.CreateAsync(new CreateProductRequest
            {
                Sku = "SKU-1",
                Name = "Product",
                Price = 100
            });

            var firstRead = await service.GetByIdAsync(created.Id);
            var secondRead = await service.GetByIdAsync(created.Id);

            var updated = await service.UpdateAsync(created.Id, new UpdateProductRequest
            {
                Name = "Product Updated",
                Price = 120
            });

            var afterUpdateRead = await service.GetByIdAsync(created.Id);

            Assert.NotNull(firstRead);
            Assert.NotNull(secondRead);
            Assert.NotNull(updated);
            Assert.NotNull(afterUpdateRead);
            Assert.Equal(created.Id, firstRead!.Id);
            Assert.Equal(100, firstRead.Price);
            Assert.Equal(100, secondRead!.Price);
            Assert.Equal(120, updated!.Price);
            Assert.Equal(120, afterUpdateRead!.Price);
            Assert.Equal(0, repository.GetByIdCalls);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReloadFromRepository_AfterTtlExpires()
        {
            var repository = new FakeProductRepository(new Product { Id = 1, Sku = "SKU-1", Name = "Keyboard", Price = 100 });
            var cache = CreateCache(ttlSeconds: 1);
            var service = CreateService(repository, cache);

            var first = await service.GetByIdAsync(1);
            await Task.Delay(1200);
            var second = await service.GetByIdAsync(1);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(2, repository.GetByIdCalls);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldKeepEntryAlive_WhenAccessedWithinSlidingTtl()
        {
            var repository = new FakeProductRepository(new Product { Id = 1, Sku = "SKU-1", Name = "Keyboard", Price = 100 });
            var cache = CreateCache(ttlSeconds: 1);
            var service = CreateService(repository, cache);

            var first = await service.GetByIdAsync(1);
            await Task.Delay(600);
            var second = await service.GetByIdAsync(1);
            await Task.Delay(600);
            var third = await service.GetByIdAsync(1);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotNull(third);
            Assert.Equal(1, repository.GetByIdCalls);
        }

        private static IProductCache CreateCache(int ttlSeconds = 4)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var settings = Options.Create(new ProductCacheSettings { TtlSeconds = ttlSeconds });
            var logger = LoggerFactory.Create(_ => { }).CreateLogger<MemoryProductCache>();
            return new MemoryProductCache(memoryCache, settings, logger);
        }

        private static ProductService CreateService(IProductRepository repository, IProductCache cache)
        {
            var logger = LoggerFactory.Create(_ => { }).CreateLogger<ProductService>();
            return new ProductService(repository, cache, logger);
        }
    }
}
