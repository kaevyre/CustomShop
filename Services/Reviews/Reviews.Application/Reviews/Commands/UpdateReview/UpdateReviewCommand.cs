using Reviews.Application.Common.Interfaces;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

/// <summary>
/// Редагування відгуку. ExpectedVersion — версія, яку бачив клієнт (If-Match або поле version);
/// якщо документ уже змінено, повертається 409.
/// </summary>
public sealed record UpdateReviewCommand : ICommand<ReviewDetailsDto>
{
    public string Id { get; init; } = "";
    public int Rating { get; init; }
    public string? Title { get; init; }
    public string Text { get; init; } = "";
    public IList<string>? Pros { get; init; }
    public IList<string>? Cons { get; init; }
    public int? ExpectedVersion { get; init; }
}
