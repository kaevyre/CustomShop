namespace Reviews.Application.Common.Models;

/// <summary>Сторінка результатів для пагінації через skip/limit.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Сторінка курсорної пагінації: наступна сторінка починається після NextCursor (ObjectId останнього документа),
/// тому вартість запиту не залежить від номера сторінки.
/// </summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
