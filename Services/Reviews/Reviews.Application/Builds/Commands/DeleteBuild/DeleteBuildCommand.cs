using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Builds.Commands.DeleteBuild;

public sealed record DeleteBuildCommand(string Id, int? ExpectedVersion = null) : ICommand;

public sealed class DeleteBuildCommandHandler(IBuildRepository builds) : IRequestHandler<DeleteBuildCommand>
{
    public async Task Handle(DeleteBuildCommand cmd, CancellationToken ct)
    {
        var id = ObjectId.Parse(cmd.Id);
        if (await builds.DeleteAsync(id, cmd.ExpectedVersion, ct))
            return;

        var current = await builds.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Build), cmd.Id);
        current.EnsureVersion(cmd.ExpectedVersion);
        throw new ConflictException($"Build '{cmd.Id}' could not be deleted");
    }
}

public sealed class DeleteBuildCommandValidator : AbstractValidator<DeleteBuildCommand>
{
    public DeleteBuildCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
    }
}
