using MongoDB.Bson;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Tests;

// Доменні правила перевіряються без жодного звернення до БД.
public class DomainTests
{
    private static readonly Author Olena = new(ObjectId.Parse("66f0c2a1e4b0a1b2c3d40001"), "Олена К.");

    private static Review NewReview() =>
        new(1, "Aurora 75", ProductType.Keyboard, Olena, new Rating(5), null, "Чудова клавіатура");

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(10)]
    public void Rating_out_of_range_throws_DomainException(int value)
        => Assert.Throws<DomainException>(() => new Rating(value));

    [Fact]
    public void Creating_review_with_rating_10_throws_DomainException()
        => Assert.Throws<DomainException>(() =>
            new Review(1, "Aurora 75", ProductType.Keyboard, Olena, new Rating(10), null, "text"));

    [Fact]
    public void Value_objects_are_compared_by_value()
    {
        Assert.Equal(new Author(Olena.ProfileId, "Олена К."), Olena);
        Assert.NotEqual(new Author(Olena.ProfileId, "Інше ім'я"), Olena);
        Assert.Equal(new Rating(4), new Rating(4));
        Assert.Equal(
            new BuildItem(2, "Glide", ProductType.Mouse, new Dictionary<string, object> { ["dpi"] = 800 }),
            new BuildItem(2, "Glide", ProductType.Mouse, new Dictionary<string, object> { ["dpi"] = 800 }));
    }

    [Fact]
    public void Author_requires_display_name()
        => Assert.Throws<DomainException>(() => new Author(ObjectId.GenerateNewId(), " "));

    [Fact]
    public void AddComment_appends_to_embedded_array_and_touches_review()
    {
        var review = NewReview();
        review.AddComment("Andrii_M", "Які стабілізатори?");

        Assert.Single(review.Comments);
        Assert.NotNull(review.UpdatedAt);
    }

    [Fact]
    public void Review_cannot_hold_more_than_50_comments()
    {
        var review = NewReview();
        for (var i = 0; i < Review.MaxComments; i++)
            review.AddComment("user", $"comment {i}");

        Assert.Throws<DomainException>(() => review.AddComment("user", "one too many"));
    }

    [Fact]
    public void Review_with_order_is_verified_purchase()
    {
        var review = new Review(1, "Aurora 75", null, Olena, new Rating(4), null, "Добре", orderId: 7);
        Assert.True(review.VerifiedPurchase);
    }

    [Fact]
    public void EnsureVersion_throws_on_stale_version()
    {
        var review = NewReview();
        review.BumpVersion();

        Assert.Throws<ConcurrencyException>(() => review.EnsureVersion(0));
        review.EnsureVersion(1);
        review.EnsureVersion(null);
    }

    [Fact]
    public void Build_requires_items_and_rejects_duplicate_products()
    {
        var item = new BuildItem(1, "Aurora 75", ProductType.Keyboard);

        Assert.Throws<DomainException>(() => new Build("Desk", null, Olena, []));
        var build = new Build("Desk", null, Olena, [item]);
        Assert.Throws<DomainException>(() => build.AddItem(item));
        Assert.Throws<DomainException>(() => build.RemoveItem(1));   // останній товар прибрати не можна
    }
}
