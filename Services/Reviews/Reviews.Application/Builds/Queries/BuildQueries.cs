using FluentValidation;
using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Builds.Queries;

public sealed record GetBuildByIdQuery(string Id) : IQuery<BuildDetailsDto>;

public sealed class GetBuildByIdQueryHandler(IBuildQueries queries) : IRequestHandler<GetBuildByIdQuery, BuildDetailsDto>
{
    public async Task<BuildDetailsDto> Handle(GetBuildByIdQuery q, CancellationToken ct)
        => await queries.GetByIdAsync(q.Id, ct) ?? throw new NotFoundException(nameof(Build), q.Id);
}

public sealed class GetBuildByIdQueryValidator : AbstractValidator<GetBuildByIdQuery>
{
    public GetBuildByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}

/// <summary>Сетапи за тегом або з товаром X (multikey-індекс по items.productId), популярні першими.</summary>
public sealed record GetBuildsListQuery(string? Tag = null, long? ProductId = null, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<BuildDto>>;

public sealed class GetBuildsListQueryHandler(IBuildQueries queries)
    : IRequestHandler<GetBuildsListQuery, PagedResult<BuildDto>>
{
    public Task<PagedResult<BuildDto>> Handle(GetBuildsListQuery q, CancellationToken ct)
        => queries.GetListAsync(q.Tag, q.ProductId, q.Page, Math.Clamp(q.PageSize, 1, 100), ct);
}

public sealed class GetBuildsListQueryValidator : AbstractValidator<GetBuildsListQuery>
{
    public GetBuildsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Tag).MaximumLength(30);
        RuleFor(x => x.ProductId).GreaterThan(0).When(x => x.ProductId is not null);
    }
}
