using ProductCatalog.API.Contracts;
using Microsoft.Extensions.Logging;
using ProductCatalog.Infrastructure.Caching;
using ProductCatalog.Infrastructure.Logging;
using ProductCatalog.Infrastructure.Models;
using ProductCatalog.Infrastructure.Repositories;

namespace ProductCatalog.API.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IProductCache _cache;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IProductRepository repository, IProductCache cache, ILogger<ProductService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ProductResponse?> GetByIdAsync(int id)
    {
        LogGetRequest(id);

        var product = await _cache.GetOrCreateAsync(id, () => _repository.GetByIdAsync(id));

        if (product is null)
        {
            LogGetNotFound(id);
            return null;
        }

        LogGetSuccess(product);
        return ToResponse(product);
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
    {
        var created = await _repository.CreateAsync(new Product
        {
            Sku = request.Sku,
            Name = request.Name,
            Price = request.Price
        });

        RefreshCache(created);
        LogCreated(created);
        return ToResponse(created);
    }

    public async Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request)
    {
        var updated = await _repository.UpdateAsync(new Product
        {
            Id = id,
            Name = request.Name,
            Price = request.Price
        });

        if (updated is null)
        {
            RemoveFromCache(id);
            LogUpdateNotFound(id);
            return null;
        }

        RefreshCache(updated);
        LogUpdated(updated);
        return ToResponse(updated);
    }

    private static string GetDisplayName(Product product) => $"{product.Name} #{product.Id}";

    private void RefreshCache(Product product) => _cache.Set(product);

    private void RemoveFromCache(int id) => _cache.Remove(id);

    private void LogGetRequest(int id) =>
        _logger.LogInformation("{Color}[PRODUCT GET REQUEST]{Reset} id={Id}", LogColors.Cyan, LogColors.Reset, id);

    private void LogGetNotFound(int id) =>
        _logger.LogInformation("{Color}[PRODUCT GET NOT FOUND]{Reset} id={Id}", LogColors.Yellow, LogColors.Reset, id);

    private void LogGetSuccess(Product product) =>
        _logger.LogInformation(
            "{Color}[PRODUCT GET SUCCESS]{Reset} displayName={DisplayName} id={Id} name={Name}",
            LogColors.Green,
            LogColors.Reset,
            GetDisplayName(product),
            product.Id,
            product.Name);

    private void LogCreated(Product product) =>
        _logger.LogInformation(
            "{Color}[PRODUCT CREATED]{Reset} displayName={DisplayName} id={Id} name={Name}",
            LogColors.Magenta,
            LogColors.Reset,
            GetDisplayName(product),
            product.Id,
            product.Name);

    private void LogUpdateNotFound(int id) =>
        _logger.LogInformation("{Color}[PRODUCT UPDATE-NOT FOUND]{Reset} id={Id}", LogColors.Yellow, LogColors.Reset, id);

    private void LogUpdated(Product product) =>
        _logger.LogInformation(
            "{Color}[PRODUCT UPDATED]{Reset} displayName={DisplayName} id={Id} name={Name}",
            LogColors.Magenta,
            LogColors.Reset,
            GetDisplayName(product),
            product.Id,
            product.Name);

    private static ProductResponse ToResponse(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Price = product.Price
    };
}
