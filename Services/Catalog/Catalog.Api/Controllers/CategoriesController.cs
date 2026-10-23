using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct) =>
        Ok(await _categories.GetAllAsync(ct));

    /// <summary>Категорія з товарами (Explicit Loading: два SQL-запити).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryWithProductsDto>> GetById(int id, CancellationToken ct) =>
        Ok(await _categories.GetWithProductsAsync(id, ct));

    /// <summary>Нещодавно додані товари категорії (LINQ через проміжну сутність M:N), рекомендовані першими.</summary>
    [HttpGet("{id:int}/recent-products")]
    public async Task<ActionResult<IReadOnlyList<CategoryProductDto>>> GetRecentProducts(
        int id, [FromQuery] DateTime? since, [FromQuery] bool featuredOnly = false, CancellationToken ct = default) =>
        Ok(await _categories.GetRecentProductsAsync(id, since, featuredOnly, ct));

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(
        CreateCategoryDto dto, [FromServices] IValidator<CreateCategoryDto> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, dto, ct) is { } invalid)
            return invalid;

        var created = await _categories.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _categories.DeleteAsync(id, ct);
        return NoContent();
    }
}
