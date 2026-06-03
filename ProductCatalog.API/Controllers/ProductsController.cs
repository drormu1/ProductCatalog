using Microsoft.AspNetCore.Mvc;
using ProductCatalog.API.Contracts;
using ProductCatalog.API.Services;
using ProductCatalog.Infrastructure.Repositories;

namespace ProductCatalog.API.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        if (id <= 0)
        {
            return BadRequest("Id must be greater than 0.");
        }

        var product = await _service.GetByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            return BadRequest("Sku is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        if (request.Price < 0)
        {
            return BadRequest("Price must be greater than or equal to 0.");
        }

        try
        {
            var created = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DuplicateSkuException)
        {
            return Conflict("A product with the same sku already exists.");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductResponse>> Update(int id, UpdateProductRequest request)
    {
        if (id <= 0)
        {
            return BadRequest("Id must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        if (request.Price < 0)
        {
            return BadRequest("Price must be greater than or equal to 0.");
        }

        var updated = await _service.UpdateAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }
}
