using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.ValueObjects;

/// <summary>
/// Автор відгуку чи сетапу — гібрид з Лаб. 01: посилання на профіль (profileId) + знімок імені (displayName).
/// </summary>
public sealed class Author : ValueObject
{
    public const int MaxDisplayNameLength = 60;

    public ObjectId ProfileId { get; }
    public string DisplayName { get; }

    public Author(ObjectId profileId, string displayName)
    {
        if (profileId == ObjectId.Empty)
            throw new DomainException("Author profileId is required");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("Author displayName is required");
        if (displayName.Trim().Length > MaxDisplayNameLength)
            throw new DomainException($"Author displayName must not exceed {MaxDisplayNameLength} characters");

        ProfileId = profileId;
        DisplayName = displayName.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProfileId;
        yield return DisplayName;
    }

    public override string ToString() => DisplayName;
}
