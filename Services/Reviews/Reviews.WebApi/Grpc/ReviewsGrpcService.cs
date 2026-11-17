using FluentValidation;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Reviews.Application.Reviews;
using Reviews.Application.Reviews.Queries.GetProductRating;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Exceptions;
using Reviews.Grpc;

namespace Reviews.WebApi.Grpc;

/// <summary>
/// gRPC-адаптер над Application-шаром (як контролер): надсилає ті самі запити через ISender, тож валідація
/// й кеш pipeline-а працюють для REST і gRPC. Винятки перетворюються на RpcException з точним кодом.
/// </summary>
public sealed class ReviewsGrpcService(ISender mediator) : ReviewsService.ReviewsServiceBase
{
    private const int MaxProducts = 100;
    private const int DefaultLatest = 3;

    public override async Task<ReviewsForProductsReply> GetForProducts(GetForProductsRequest request, ServerCallContext context)
    {
        if (request.ProductIds.Count == 0 || request.ProductIds.Count > MaxProducts)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"ProductIds must contain from 1 to {MaxProducts} items"));
        if (request.ProductIds.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Every product id must be positive"));

        var latest = request.LatestPerProduct > 0 ? request.LatestPerProduct : DefaultLatest;
        var ct = context.CancellationToken;

        try
        {
            var products = await Task.WhenAll(request.ProductIds.Distinct().Select(id => GetProductReviewsAsync(id, latest, ct)));

            var reply = new ReviewsForProductsReply();
            reply.Products.AddRange(products);
            return reply;
        }
        catch (ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<ProductRatingReply> GetProductRating(GetProductRatingRequest request, ServerCallContext context)
    {
        if (request.ProductId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ProductId must be positive"));

        try
        {
            var rating = await mediator.Send(new GetProductRatingQuery(request.ProductId), context.CancellationToken);
            return new ProductRatingReply
            {
                ProductId = rating.ProductId,
                ReviewsCount = rating.ReviewsCount,
                AverageRating = rating.AverageRating
            };
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    private async Task<ProductReviews> GetProductReviewsAsync(long productId, int latest, CancellationToken ct)
    {
        var ratingTask = GetRatingOrNullAsync(productId, ct);
        var pageTask = mediator.Send(new GetReviewsListQuery(productId, Page: 1, PageSize: latest), ct);
        await Task.WhenAll(ratingTask, pageTask);

        var page = await pageTask;
        var reply = new ProductReviews { ProductId = productId, TotalCount = page.TotalCount };

        if (await ratingTask is { } rating)
            reply.AverageRating = rating;

        reply.Latest.AddRange(page.Items.Select(ToReply));
        return reply;
    }

    private async Task<double?> GetRatingOrNullAsync(long productId, CancellationToken ct)
    {
        try
        {
            return (await mediator.Send(new GetProductRatingQuery(productId), ct)).AverageRating;
        }
        catch (NotFoundException)
        {
            return null;                                           // відгуків на товар ще немає
        }
    }

    private static ReviewReply ToReply(ReviewDto review)
    {
        var reply = new ReviewReply
        {
            Id = review.Id,
            ProductId = review.ProductId,
            AuthorName = review.AuthorName,
            Rating = review.Rating,
            Text = review.Text,
            VerifiedPurchase = review.VerifiedPurchase,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Utc))
        };

        if (review.Title is not null)
            reply.Title = review.Title;
        return reply;
    }
}
