using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

/// <summary>
/// Працює лише через Unit of Work. Правила самої сутності (ціна &gt; 0, достатній залишок) перевіряє Product;
/// сервіс перевіряє правила, для яких потрібні інші дані з БД: унікальність SKU і slug, існування бренду та категорії.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ProductService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<PagedResult<ProductDto>> GetListAsync(ProductQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, PageRequest.MaxPageSize);
        var spec = new ProductsFilterSpec(query, page, pageSize);

        var total = await _uow.Products.CountAsync(spec, ct);     // SELECT COUNT(*) з тими самими фільтрами
        var items = await _uow.Products.ListAsync(spec, ct);      // ORDER BY … OFFSET … FETCH

        return new PagedResult<ProductDto>(_mapper.Map<List<ProductDto>>(items), total, page, pageSize);
    }

    public async Task<ProductDetailsDto> GetByIdAsync(long id, CancellationToken ct = default) =>
        _mapper.Map<ProductDetailsDto>(await LoadAsync(id, ct));

    public async Task<ProductDetailsDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        var sku = dto.Sku.Trim();
        if (await _uow.Products.ExistsBySkuAsync(sku, ct))
            throw new ConflictException($"Product with SKU '{sku}' already exists.");
        if (await _uow.Products.ExistsBySlugAsync(dto.Slug, ct))
            throw new ConflictException($"Product with slug '{dto.Slug}' already exists.");
        if (await _uow.Brands.GetByIdAsync(dto.BrandId, ct) is null)
            throw new EntityNotFoundException(nameof(Brand), dto.BrandId);

        var product = new Product(dto.BrandId, sku, dto.Name, dto.Slug, dto.ProductType, dto.BasePrice,
            dto.Description, dto.IsCustomizable);
        if (dto.InitialStock > 0)
            product.AddStock(dto.InitialStock);

        await _uow.Products.AddAsync(product, ct);
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(product.Id, ct);
    }

    public async Task<ProductDto> ChangePriceAsync(long id, ChangePriceDto dto, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        if (!string.IsNullOrEmpty(dto.RowVersion))
            _uow.Products.SetOriginalRowVersion(product, Convert.FromBase64String(dto.RowVersion));

        product.ChangePrice(dto.Price);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> AddStockAsync(long id, StockChangeDto dto, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        product.AddStock(dto.Quantity);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> RemoveStockAsync(long id, StockChangeDto dto, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        product.RemoveStock(dto.Quantity);                        // недостатньо → DomainException → 409
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task AssignCategoryAsync(long id, AssignCategoryDto dto, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        if (await _uow.Categories.GetByIdAsync(dto.CategoryId, ct) is null)
            throw new EntityNotFoundException(nameof(Category), dto.CategoryId);

        product.AddToCategory(dto.CategoryId, dto.IsFeatured, DateTime.UtcNow);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task RemoveCategoryAsync(long id, int categoryId, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        product.RemoveFromCategory(categoryId);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);

        product.Delete();                                         // м'яке видалення: Id товару копіюють інші сервіси
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Product> LoadAsync(long id, CancellationToken ct) =>
        await _uow.Products.GetWithDetailsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);
}
