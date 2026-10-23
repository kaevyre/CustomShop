using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/brands")]
public sealed class BrandsController : ControllerBase
{
    private readonly IBrandService _brands;

    public BrandsController(IBrandService brands)
    {
        _brands = brands;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> GetAll(CancellationToken ct) =>
        Ok(await _brands.GetAllAsync(ct));

    [HttpGet("{id:int}/products")]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts(
        int id, [FromQuery] PageRequest paging, CancellationToken ct) =>
        Ok(await _brands.GetProductsAsync(id, paging, ct));
}
