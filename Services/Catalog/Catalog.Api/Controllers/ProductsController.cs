using Catalog.Api.Metrics;
using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _products;
    private readonly CatalogMetrics _metrics;

    public ProductsController(IProductService products, CatalogMetrics metrics)
    {
        _products = products;
        _metrics = metrics;
    }

    /// <summary>Список з пагінацією, фільтрами й сортуванням; метадані — в тілі і в заголовках X-Total-Count, X-Total-Pages.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetList(
        [FromQuery] ProductQuery query, [FromServices] IValidator<ProductQuery> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, query, ct) is { } invalid)
            return invalid;

        var result = await _products.GetListAsync(query, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        Response.Headers["X-Total-Pages"] = result.TotalPages.ToString();
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProductDetailsDto>> GetById(long id, CancellationToken ct) =>
        Ok(await _metrics.MeasureAsync("get", () => _products.GetByIdAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ProductDetailsDto>> Create(
        CreateProductDto dto, [FromServices] IValidator<CreateProductDto> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, dto, ct) is { } invalid)
            return invalid;

        var created = await _metrics.MeasureAsync("create", () => _products.CreateAsync(dto, ct));
        _metrics.ProductCreated(created.ProductType);       // лічильник — лише після успішного створення
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}/price")]
    public async Task<ActionResult<ProductDto>> ChangePrice(
        long id, ChangePriceDto dto, [FromServices] IValidator<ChangePriceDto> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, dto, ct) is { } invalid)
            return invalid;

        var product = await _metrics.MeasureAsync("change_price", () => _products.ChangePriceAsync(id, dto, ct));
        _metrics.ProductUpdated("price");
        return Ok(product);
    }

    /// <summary>Перейменування товару: після збереження публікується ProductRenamedEvent (Відгуки оновлюють productName).</summary>
    [HttpPut("{id:long}/name")]
    public async Task<ActionResult<ProductDto>> Rename(
        long id, RenameProductDto dto, [FromServices] IValidator<RenameProductDto> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, dto, ct) is { } invalid)
            return invalid;

        var product = await _metrics.MeasureAsync("rename", () => _products.RenameAsync(id, dto, ct));
        _metrics.ProductUpdated("name");
        return Ok(product);
    }

    [HttpPost("{id:long}/stock/add")]
    public async Task<ActionResult<ProductDto>> AddStock(long id, StockChangeDto dto, CancellationToken ct)
    {
        var product = await _metrics.MeasureAsync("stock", () => _products.AddStockAsync(id, dto, ct));
        _metrics.ProductUpdated("stock_add");
        return Ok(product);
    }

    [HttpPost("{id:long}/stock/remove")]
    public async Task<ActionResult<ProductDto>> RemoveStock(long id, StockChangeDto dto, CancellationToken ct)
    {
        var product = await _metrics.MeasureAsync("stock", () => _products.RemoveStockAsync(id, dto, ct));
        _metrics.ProductUpdated("stock_remove");
        return Ok(product);
    }

    [HttpPost("{id:long}/categories")]
    public async Task<IActionResult> AssignCategory(long id, AssignCategoryDto dto, CancellationToken ct)
    {
        await _products.AssignCategoryAsync(id, dto, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}/categories/{categoryId:int}")]
    public async Task<IActionResult> RemoveCategory(long id, int categoryId, CancellationToken ct)
    {
        await _products.RemoveCategoryAsync(id, categoryId, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _products.DeleteAsync(id, ct);
        return NoContent();
    }
}
