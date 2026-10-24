using AutoMapper;
using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Builds.Commands.UpdateBuild;

public sealed record UpdateBuildCommand : ICommand<BuildDetailsDto>
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public IList<string>? Tags { get; init; }
    public int? ExpectedVersion { get; init; }
}

public sealed class UpdateBuildCommandHandler(IBuildRepository builds, IMapper mapper)
    : IRequestHandler<UpdateBuildCommand, BuildDetailsDto>
{
    public async Task<BuildDetailsDto> Handle(UpdateBuildCommand cmd, CancellationToken ct)
    {
        var build = await builds.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
            ?? throw new NotFoundException(nameof(Build), cmd.Id);

        build.EnsureVersion(cmd.ExpectedVersion);
        build.UpdateDetails(cmd.Title, cmd.Description, cmd.Tags);

        await builds.UpdateAsync(build, ct);
        return mapper.Map<BuildDetailsDto>(build);
    }
}

public sealed class UpdateBuildCommandValidator : AbstractValidator<UpdateBuildCommand>
{
    public UpdateBuildCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.Title).NotEmpty().Length(Build.MinTitleLength, Build.MaxTitleLength);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Tags).MustBeShortList(maxLength: 30);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
    }
}
