using FluentValidation;
using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Reviews.Queries.GetReviewById;

public sealed record GetReviewByIdQuery(string Id) : IQuery<ReviewDetailsDto>;

public sealed class GetReviewByIdQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewByIdQuery, ReviewDetailsDto>
{
    public async Task<ReviewDetailsDto> Handle(GetReviewByIdQuery q, CancellationToken ct)
        => await queries.GetByIdAsync(q.Id, ct) ?? throw new NotFoundException(nameof(Review), q.Id);
}

public sealed class GetReviewByIdQueryValidator : AbstractValidator<GetReviewByIdQuery>
{
    public GetReviewByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}
