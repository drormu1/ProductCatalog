namespace ProductCatalog.Infrastructure.Repositories;

public class DuplicateSkuException : Exception
{
    public DuplicateSkuException(string sku)
        : base($"A product with sku '{sku}' already exists.")
    {
        Sku = sku;
    }

    public string Sku { get; }
}
