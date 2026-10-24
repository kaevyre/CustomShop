using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Entities;

/// <summary>
/// Короткий коментар, вбудований у відгук (масив reviews.comments з Лаб. 01).
/// Власного _id не має: живе лише всередині агрегату Review і змінюється тільки через нього.
/// </summary>
public sealed class Comment
{
    public const int MaxTextLength = 1000;

    public string Author { get; private set; } = "";
    public string Text { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }

    private Comment() { }                                   // для десеріалізації

    internal Comment(string author, string text, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(author))
            throw new DomainException("Comment author is required");
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Comment text is required");
        if (text.Length > MaxTextLength)
            throw new DomainException($"Comment text must not exceed {MaxTextLength} characters");

        Author = author.Trim();
        Text = text.Trim();
        CreatedAt = createdAt;
    }
}
