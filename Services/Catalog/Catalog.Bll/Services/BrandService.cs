using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

public sealed class BrandService : IBrandService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public BrandService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken ct = default)
    {
        var brands = await _uow.Brands.GetAllAsync(ct);
        return _mapper.Map<List<BrandDto>>(brands.OrderBy(b => b.Name));
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(int brandId, PageRequest paging, CancellationToken ct = default)
    {
        if (await _uow.Brands.GetByIdAsync(brandId, ct) is null)
            throw new EntityNotFoundException(nameof(Brand), brandId);

        var page = Math.Max(paging.Page, 1);
        var pageSize = Math.Clamp(paging.PageSize, 1, PageRequest.MaxPageSize);
        var spec = new ProductsByBrandSpec(brandId, page, pageSize);

        var total = await _uow.Products.CountAsync(spec, ct);
        var items = await _uow.Products.ListAsync(spec, ct);
        return new PagedResult<ProductDto>(_mapper.Map<List<ProductDto>>(items), total, page, pageSize);
    }
}
