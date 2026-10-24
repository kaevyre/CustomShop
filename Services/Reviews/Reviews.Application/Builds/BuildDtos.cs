using Reviews.Application.Reviews;

namespace Reviews.Application.Builds;

/// <summary>Рядок списку сетапів (проєкція в БД).</summary>
public sealed record BuildDto(
    string Id,
    string Title,
    string AuthorName,
    int ItemsCount,
    IReadOnlyList<string> Tags,
    int Likes,
    DateTime CreatedAt,
    int Version);

public sealed record BuildItemDto(
    long ProductId,
    string ProductName,
    string ProductType,
    IReadOnlyDictionary<string, object> Customization,
    string? Note);

public sealed record BuildDetailsDto
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public AuthorDto Author { get; init; } = null!;
    public IReadOnlyList<BuildItemDto> Items { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Photos { get; init; } = [];
    public int Likes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int Version { get; init; }
}
