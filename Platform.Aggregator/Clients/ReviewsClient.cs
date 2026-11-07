using System.Diagnostics;
using System.Net;

namespace Platform.Aggregator.Clients;

/// <summary>Typed HttpClient для reviews-api (BaseAddress = https+http://reviews-api).</summary>
public sealed class ReviewsClient(HttpClient http, ILogger<ReviewsClient> logger)
{
    public const string ServiceName = "reviews-api";
    private const int LatestReviews = 3;

    /// <summary>Рейтинг і останні відгуки товару: два незалежні запити виконуються паралельно.</summary>
    public async Task<ProductReviewsDto> GetForProductAsync(long productId, CancellationToken ct)
    {
        logger.LogInformation("Calling {Service} for reviews of product {ProductId}", ServiceName, productId);
        var sw = Stopwatch.StartNew();

        var ratingTask = GetRatingAsync(productId, ct);
        var pageTask = http.GetFromJsonAsync<ReviewPageDto>(
            $"/api/reviews?productId={productId}&page=1&pageSize={LatestReviews}", ct);
        await Task.WhenAll(ratingTask, pageTask);

        var page = await pageTask;
        logger.LogInformation("{Service} responded for product {ProductId} in {ElapsedMs} ms",
            ServiceName, productId, sw.ElapsedMilliseconds);

        return new ProductReviewsDto(productId, await ratingTask, page?.Items ?? [], page?.TotalCount ?? 0);
    }

    public async Task<IReadOnlyList<ProductReviewsDto>> GetForProductsAsync(IReadOnlyCollection<long> productIds, CancellationToken ct) =>
        await Task.WhenAll(productIds.Select(id => GetForProductAsync(id, ct)));

    private async Task<ProductRatingDto?> GetRatingAsync(long productId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/reviews/products/{productId}/rating", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;                                           // відгуків на товар ще немає

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductRatingDto>(ct);
    }
}
