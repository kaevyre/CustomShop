namespace Reviews.WebApi.Contracts;

/// <summary>Тіло PUT /api/reviews/{id}; Id береться з маршруту, версія — з If-Match або поля version.</summary>
public sealed record UpdateReviewRequest
{
    public int Rating { get; init; }
    public string? Title { get; init; }
    public string Text { get; init; } = "";
    public IList<string>? Pros { get; init; }
    public IList<string>? Cons { get; init; }
    public int? Version { get; init; }
}

public sealed record AddCommentRequest(string Author, string Text);

public sealed record UpdateBuildRequest
{
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public IList<string>? Tags { get; init; }
    public int? Version { get; init; }
}

public sealed record CreatedResponse(string Id);

public sealed record LikesResponse(int Likes);
