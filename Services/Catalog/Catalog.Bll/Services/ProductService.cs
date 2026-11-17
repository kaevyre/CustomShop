using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Catalog.Bll.Caching;
using Catalog.Bll.Telemetry;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Services;

/// <summary>
/// Працює лише через Unit of Work. Правила самої сутності (ціна &gt; 0, достатній залишок) перевіряє Product;
/// сервіс перевіряє правила, для яких потрібні інші дані з БД: унікальність SKU і slug, існування бренду та категорії.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<ProductService> _logger;
    private readonly ITwoLevelCache _cache;

    public ProductService(IUnitOfWork uow, IMapper mapper, ILogger<ProductService> logger, ITwoLevelCache cache)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
        _cache = cache;
    }

    public async Task<PagedResult<ProductDto>> GetListAsync(ProductQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, PageRequest.MaxPageSize);
        var version = await _cache.GetVersionAsync(CatalogCacheKeys.ProductListsVersion, ct);

        return (await _cache.GetOrCreateAsync(
            CatalogCacheKeys.ProductsList(version, query, page, pageSize),
            CatalogCacheProfiles.List(pageSize),
            async cancellation =>
            {
                var spec = new ProductsFilterSpec(query, page, pageSize);
                var total = await _uow.Products.CountAsync(spec, cancellation);     // SELECT COUNT(*) з тими самими фільтрами
                var items = await _uow.Products.ListAsync(spec, cancellation);      // ORDER BY … OFFSET … FETCH
                return new PagedResult<ProductDto>(_mapper.Map<List<ProductDto>>(items), total, page, pageSize);
            },
            ct))!;
    }

    public async Task<ProductDetailsDto> GetByIdAsync(long id, CancellationToken ct = default) =>
        await GetCachedAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Product), id);

    /// <summary>Пакетне читання для gRPC (Aggregator): той самий кеш, що й у REST; відсутні товари пропускаються.</summary>
    public async Task<IReadOnlyList<ProductDetailsDto>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        var products = new List<ProductDetailsDto>(ids.Count);
        foreach (var id in ids)                                   // послідовно: один DbContext на запит
        {
            if (await GetCachedAsync(id, ct) is { } product)
                products.Add(product);
        }

        return products;
    }

    private Task<ProductDetailsDto?> GetCachedAsync(long id, CancellationToken ct) =>
        _cache.GetOrCreateAsync(CatalogCacheKeys.Product(id), CatalogCacheProfiles.Product, async cancellation =>
        {
            var product = await _uow.Products.GetWithDetailsAsync(id, cancellation);
            return product is null ? null : _mapper.Map<ProductDetailsDto>(product);
        }, ct);

    /// <summary>Після успішного запису: картка товару і всі списки, у які він входить.</summary>
    private async Task InvalidateProductAsync(long id, string reason)
    {
        await _cache.InvalidateAsync(reason, CatalogCacheKeys.Product(id));
        await _cache.BumpVersionAsync(CatalogCacheKeys.ProductListsVersion, reason);
    }

    public async Task<ProductDetailsDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        using var activity = CatalogTelemetry.Source.StartActivity("CreateProduct");
        var sku = dto.Sku.Trim();
        activity?.SetTag("product.sku", sku);

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

        activity?.SetTag("product.id", product.Id);
        _logger.LogInformation("Product {ProductId} created: SKU {Sku}, type {ProductType}, price {Price}, stock {Stock}",
            product.Id, sku, dto.ProductType, dto.BasePrice, dto.InitialStock);

        await InvalidateProductAsync(product.Id, "ProductCreated");
        return await GetByIdAsync(product.Id, ct);
    }

    public async Task<ProductDto> ChangePriceAsync(long id, ChangePriceDto dto, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        if (!string.IsNullOrEmpty(dto.RowVersion))
            _uow.Products.SetOriginalRowVersion(product, Convert.FromBase64String(dto.RowVersion));

        using var activity = CatalogTelemetry.Source.StartActivity("ChangeProductPrice");
        activity?.SetTag("product.id", id);

        product.ChangePrice(dto.Price);
        await _uow.SaveChangesAsync(ct);
        await InvalidateProductAsync(id, "ProductPriceChanged");
        _logger.LogInformation("Product {ProductId} price changed to {Price}", id, dto.Price);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> AddStockAsync(long id, StockChangeDto dto, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        product.AddStock(dto.Quantity);
        await _uow.SaveChangesAsync(ct);
        await InvalidateProductAsync(id, "ProductStockChanged");
        _logger.LogInformation("Product {ProductId} stock increased by {Quantity} to {Stock}", id, dto.Quantity, product.Stock);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> RemoveStockAsync(long id, StockChangeDto dto, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        using var activity = CatalogTelemetry.Source.StartActivity("RemoveProductStock");
        activity?.SetTag("product.id", id);
        activity?.SetTag("stock.quantity", dto.Quantity);

        product.RemoveStock(dto.Quantity);                        // недостатньо → DomainException → 409
        await _uow.SaveChangesAsync(ct);
        await InvalidateProductAsync(id, "ProductStockChanged");
        _logger.LogInformation("Product {ProductId} stock decreased by {Quantity} to {Stock}", id, dto.Quantity, product.Stock);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task AssignCategoryAsync(long id, AssignCategoryDto dto, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        if (await _uow.Categories.GetByIdAsync(dto.CategoryId, ct) is null)
            throw new EntityNotFoundException(nameof(Category), dto.CategoryId);

        product.AddToCategory(dto.CategoryId, dto.IsFeatured, DateTime.UtcNow);
        await _uow.SaveChangesAsync(ct);
        await InvalidateProductAsync(id, "ProductCategoryAssigned");
    }

    public async Task RemoveCategoryAsync(long id, int categoryId, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        product.RemoveFromCategory(categoryId);
        await _uow.SaveChangesAsync(ct);
        await InvalidateProductAsync(id, "ProductCategoryRemoved");
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);

        product.Delete();                                         // м'яке видалення: Id товару копіюють інші сервіси
        await _uow.SaveChangesAsync(ct);
        await InvalidateProductAsync(id, "ProductDeleted");
        _logger.LogInformation("Product {ProductId} deleted (soft)", id);
    }

    private async Task<Product> LoadAsync(long id, CancellationToken ct) =>
        await _uow.Products.GetWithDetailsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);
}
