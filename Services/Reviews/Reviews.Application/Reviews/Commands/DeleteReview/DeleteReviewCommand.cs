using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

/// <summary>Видалення відгуку; з ExpectedVersion — лише якщо документ не змінювався.</summary>
public sealed record DeleteReviewCommand(string Id, int? ExpectedVersion = null) : ICommand;

public sealed class DeleteReviewCommandHandler(IReviewRepository reviews) : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand cmd, CancellationToken ct)
    {
        var id = ObjectId.Parse(cmd.Id);
        if (await reviews.DeleteAsync(id, cmd.ExpectedVersion, ct))
            return;

        // нічого не видалено: або документа немає (404), або версія вже інша (409)
        var current = await reviews.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Review), cmd.Id);
        current.EnsureVersion(cmd.ExpectedVersion);
        throw new ConflictException($"Review '{cmd.Id}' could not be deleted");
    }
}

public sealed class DeleteReviewCommandValidator : AbstractValidator<DeleteReviewCommand>
{
    public DeleteReviewCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
    }
}
