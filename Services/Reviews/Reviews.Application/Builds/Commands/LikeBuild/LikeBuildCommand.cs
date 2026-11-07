using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Builds.Commands.LikeBuild;

/// <summary>«Подобається» сетапу; повертає нову кількість лайків.</summary>
public sealed record LikeBuildCommand(string Id) : ICommand<int>;

public sealed class LikeBuildCommandHandler(IBuildRepository builds) : IRequestHandler<LikeBuildCommand, int>
{
    public async Task<int> Handle(LikeBuildCommand cmd, CancellationToken ct)
    {
        var build = await builds.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
            ?? throw new NotFoundException(nameof(Build), cmd.Id);

        build.Like();
        await builds.UpdateAsync(build, ct);
        return build.Likes;
    }
}

public sealed class LikeBuildCommandValidator : AbstractValidator<LikeBuildCommand>
{
    public LikeBuildCommandValidator() => RuleFor(x => x.Id).MustBeObjectId();
}
