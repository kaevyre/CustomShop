using AutoMapper;
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

    public CategoryService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _uow.Categories.GetAllAsync(ct);
        return _mapper.Map<List<CategoryDto>>(categories.OrderBy(c => c.SortOrder));
    }

    public async Task<CategoryWithProductsDto> GetWithProductsAsync(int id, CancellationToken ct = default)
    {
        var category = await _uow.Categories.GetWithProductsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);
        return _mapper.Map<CategoryWithProductsDto>(category);
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
        return _mapper.Map<CategoryDto>(category);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _uow.Categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        category.Delete();
        await _uow.SaveChangesAsync(ct);
    }
}
