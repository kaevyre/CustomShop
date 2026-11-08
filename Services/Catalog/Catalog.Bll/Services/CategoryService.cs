using AutoMapper;
using Catalog.Bll.Caching;
using Catalog.Bll.Dtos;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

public sealed class CategoryService : ICategoryService
{
    private static readonly TimeSpan DefaultRecentPeriod = TimeSpan.FromDays(90);

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ITwoLevelCache _cache;

    public CategoryService(IUnitOfWork uow, IMapper mapper, ITwoLevelCache cache)
    {
        _uow = uow;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _cache.GetOrCreateAsync<List<CategoryDto>>(CatalogCacheKeys.CategoriesAll, CatalogCacheProfiles.Reference,
            async cancellation =>
            {
                var categories = await _uow.Categories.GetAllAsync(cancellation);
                return _mapper.Map<List<CategoryDto>>(categories.OrderBy(c => c.SortOrder));
            }, ct))!;

    public async Task<CategoryWithProductsDto> GetWithProductsAsync(int id, CancellationToken ct = default)
    {
        // склад і ціни товарів у категорії залежать від тих самих змін, що й списки товарів
        var version = await _cache.GetVersionAsync(CatalogCacheKeys.ProductListsVersion, ct);

        return await _cache.GetOrCreateAsync(CatalogCacheKeys.Category(id, version), CatalogCacheProfiles.List(PageRequest.DefaultPageSize),
                async cancellation =>
                {
                    var category = await _uow.Categories.GetWithProductsAsync(id, cancellation);
                    return category is null ? null : _mapper.Map<CategoryWithProductsDto>(category);
                }, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);
    }

    public async Task<IReadOnlyList<CategoryProductDto>> GetRecentProductsAsync(
        int id, DateTime? since, bool featuredOnly, CancellationToken ct = default)
    {
        var from = since ?? DateTime.UtcNow - DefaultRecentPeriod;
        if (from > DateTime.UtcNow)
            throw new ValidationException("since", "Date 'since' cannot be in the future.");

        if (await _uow.Categories.GetByIdAsync(id, ct) is null)
            throw new EntityNotFoundException(nameof(Category), id);

        var links = await _uow.ProductCategories.GetRecentlyAddedAsync(id, from, featuredOnly, ct);
        return _mapper.Map<List<CategoryProductDto>>(links);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default)
    {
        if (await _uow.Categories.ExistsBySlugAsync(dto.Slug, ct))
            throw new ConflictException($"Category with slug '{dto.Slug}' already exists.");

        var category = new Category(dto.Name, dto.Slug, dto.SortOrder);
        await _uow.Categories.AddAsync(category, ct);
        await _uow.SaveChangesAsync(ct);

        await _cache.InvalidateAsync("CategoryCreated", CatalogCacheKeys.CategoriesAll);
        return _mapper.Map<CategoryDto>(category);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        // разом із товарами: їхні картки в кеші містять назву цієї категорії
        var category = await _uow.Categories.GetWithProductsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);
        var productKeys = category.ProductCategories.Select(link => CatalogCacheKeys.Product(link.ProductId)).ToArray();

        category.Delete();
        await _uow.SaveChangesAsync(ct);

        await _cache.InvalidateAsync("CategoryDeleted", [CatalogCacheKeys.CategoriesAll, .. productKeys]);
        await _cache.BumpVersionAsync(CatalogCacheKeys.ProductListsVersion, "CategoryDeleted");
    }
}
