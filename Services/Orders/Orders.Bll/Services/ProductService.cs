using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Bll.Validation;
using Orders.Dal.Abstractions;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Bll.Services;

public sealed class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ProductService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<ProductDto> GetAsync(long id, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Product), id);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<PagedResultDto<ProductDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        RequestRules.EnsurePaging(page, pageSize);

        var result = await _uow.Products.GetPagedAsync(page, pageSize, ct);
        return new PagedResultDto<ProductDto>
        {
            Items = _mapper.Map<List<ProductDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        if (await _uow.Products.GetByIdAsync(dto.Id, ct) is not null)
            throw new BusinessConflictException($"Product with id '{dto.Id}' already exists.");

        var product = _mapper.Map<Product>(dto);
        product.Sku = product.Sku.Trim();
        product.Name = product.Name.Trim();

        if (await _uow.Products.GetBySkuAsync(product.Sku, ct) is not null)
            throw new BusinessConflictException($"Product with SKU '{product.Sku}' already exists.");

        product.SyncedAt = DateTime.UtcNow;
        await _uow.Products.AddAsync(product, ct);

        return await GetAsync(product.Id, ct);
    }

    public async Task<ProductDto> UpdateAsync(long id, UpdateProductDto dto, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Product), id);

        var expectedRowVersion = RequestRules.ParseRowVersion(dto.RowVersion);

        _mapper.Map(dto, product);
        product.Sku = product.Sku.Trim();
        product.Name = product.Name.Trim();
        product.SyncedAt = DateTime.UtcNow;

        var sameSku = await _uow.Products.GetBySkuAsync(product.Sku, ct);
        if (sameSku is not null && sameSku.Id != id)
            throw new BusinessConflictException($"Product with SKU '{product.Sku}' already exists.");

        if (expectedRowVersion is not null)
            product.RowVersion = expectedRowVersion;

        var updated = await _uow.Products.UpdateAsync(product, ct);
        if (!updated)
            throw new BusinessConflictException("The product was modified by someone else. Reload it and try again.");

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var deleted = await _uow.Products.DeleteAsync(id, ct);
        if (!deleted)
            throw new NotFoundException(nameof(Product), id);
    }
}
