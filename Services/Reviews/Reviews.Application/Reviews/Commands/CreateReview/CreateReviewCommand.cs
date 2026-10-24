using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Enums;

namespace Reviews.Application.Reviews.Commands.CreateReview;

/// <summary>Створення відгуку; повертає рядковий ObjectId нового документа.</summary>
public sealed record CreateReviewCommand : ICommand<string>
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = "";
    public ProductType? ProductType { get; init; }
    public string AuthorProfileId { get; init; } = "";
    public string AuthorName { get; init; } = "";
    public int Rating { get; init; }
    public string? Title { get; init; }
    public string Text { get; init; } = "";
    public IList<string>? Pros { get; init; }
    public IList<string>? Cons { get; init; }
    public IDictionary<string, string>? Customization { get; init; }
    public long? OrderId { get; init; }
}
