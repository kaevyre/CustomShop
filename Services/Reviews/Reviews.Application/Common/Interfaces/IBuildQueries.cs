using Reviews.Application.Builds;
using Reviews.Application.Common.Models;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Read-side сетапів; реалізація в Infrastructure.</summary>
public interface IBuildQueries
{
    Task<BuildDetailsDto?> GetByIdAsync(string id, CancellationToken ct);
    Task<PagedResult<BuildDto>> GetListAsync(string? tag, long? productId, int page, int pageSize, CancellationToken ct);
}
