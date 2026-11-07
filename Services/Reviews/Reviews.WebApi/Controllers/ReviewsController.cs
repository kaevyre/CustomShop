using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Queries.GetProductRating;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Application.Reviews.Queries.GetReviewsFeed;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.WebApi.Contracts;

namespace Reviews.WebApi.Controllers;

[Route("api/reviews")]
public sealed class ReviewsController : ApiControllerBase
{
    /// <summary>Список відгуків: фільтри productId / authorProfileId / minRating, повнотекстовий search, пагінація.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ReviewDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetList(
        [FromQuery] long? productId, [FromQuery] string? authorProfileId, [FromQuery] int? minRating,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(
            new GetReviewsListQuery(productId, authorProfileId, minRating, search, page, pageSize), ct));

    /// <summary>Стрічка «нові першими» з курсорною пагінацією: after = nextCursor з попередньої сторінки.</summary>
    [HttpGet("feed")]
    [ProducesResponseType<CursorPage<ReviewDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CursorPage<ReviewDto>>> GetFeed(
        [FromQuery] long? productId, [FromQuery] string? after, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetReviewsFeedQuery(productId, after, pageSize), ct));

    /// <summary>Повний відгук; версія документа — у заголовку ETag.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<ReviewDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDetailsDto>> GetById(string id, CancellationToken ct)
    {
        var review = await Mediator.Send(new GetReviewByIdQuery(id), ct);
        SetETag(review.Version);
        return Ok(review);
    }

    /// <summary>Середня оцінка й розподіл оцінок товару.</summary>
    [HttpGet("products/{productId:long}/rating")]
    [ProducesResponseType<ProductRatingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductRatingDto>> GetProductRating(long productId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetProductRatingQuery(productId), ct));

    /// <summary>Створення відгуку; 201 + Location з рядковим ObjectId. Повторний відгук автора на товар — 400/409.</summary>
    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateReviewCommand command, CancellationToken ct)
    {
        var id = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(id));
    }

    /// <summary>Редагування; очікувана версія — If-Match: "N" (або поле version). Застаріла версія — 409.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType<ReviewDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDetailsDto>> Update(string id, UpdateReviewRequest request, CancellationToken ct)
    {
        var command = Mapper.Map<UpdateReviewCommand>(request) with
        {
            Id = id,
            ExpectedVersion = ExpectedVersion(request.Version)
        };
        var review = await Mediator.Send(command, ct);
        SetETag(review.Version);
        return Ok(review);
    }

    /// <summary>Додає коментар у вкладений масив comments.</summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType<ReviewDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDetailsDto>> AddComment(string id, AddCommentRequest request, CancellationToken ct)
    {
        var review = await Mediator.Send(new AddCommentCommand(id, request.Author, request.Text), ct);
        SetETag(review.Version);
        return Ok(review);
    }

    /// <summary>Видалення; з If-Match — лише якщо документ не змінювався.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteReviewCommand(id, ExpectedVersion()), ct);
        return NoContent();
    }
}
