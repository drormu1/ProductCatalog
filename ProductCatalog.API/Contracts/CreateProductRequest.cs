namespace ProductCatalog.API.Contracts;

public class CreateProductRequest
{
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }
}
