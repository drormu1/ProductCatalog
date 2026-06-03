using ProductCatalog.API.Contracts;

namespace ProductCatalog.API.Services;

public interface IProductService
{
    Task<ProductResponse?> GetByIdAsync(int id);

    Task<ProductResponse> CreateAsync(CreateProductRequest request);

    Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request);
}
