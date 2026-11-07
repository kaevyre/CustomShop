using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Builds.Commands.CreateBuild;

public sealed record BuildItemInput
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = "";
    public ProductType? ProductType { get; init; }
    public IDictionary<string, string>? Customization { get; init; }
    public string? Note { get; init; }
}

/// <summary>Публікація сетапу; повертає рядковий ObjectId.</summary>
public sealed record CreateBuildCommand : ICommand<string>
{
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string AuthorProfileId { get; init; } = "";
    public string AuthorName { get; init; } = "";
    public IList<BuildItemInput> Items { get; init; } = [];
    public IList<string>? Tags { get; init; }
}

public sealed class CreateBuildCommandHandler(IBuildRepository builds) : IRequestHandler<CreateBuildCommand, string>
{
    public async Task<string> Handle(CreateBuildCommand cmd, CancellationToken ct)
    {
        var items = cmd.Items.Select(i => new BuildItem(
            i.ProductId, i.ProductName, i.ProductType!.Value,
            i.Customization?.ToDictionary(p => p.Key, p => (object)p.Value), i.Note));

        var build = new Build(cmd.Title, cmd.Description,
            new Author(ObjectId.Parse(cmd.AuthorProfileId), cmd.AuthorName), items, cmd.Tags);

        await builds.AddAsync(build, ct);
        return build.Id.ToString();
    }
}

public sealed class CreateBuildCommandValidator : AbstractValidator<CreateBuildCommand>
{
    public CreateBuildCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(Build.MinTitleLength, Build.MaxTitleLength);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.AuthorProfileId).MustBeObjectId();
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(Author.MaxDisplayNameLength);
        RuleFor(x => x.Tags).MustBeShortList(maxLength: 30);
        RuleFor(x => x.Items).NotEmpty()
            .Must(items => items.Count <= Build.MaxItems).WithMessage($"Build cannot contain more than {Build.MaxItems} items")
            .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Each product can appear in a build only once");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.ProductName).NotEmpty().MaximumLength(200);
            item.RuleFor(i => i.ProductType).NotNull().IsInEnum();
            item.RuleFor(i => i.Customization).MustBeValidSubdocument();
            item.RuleFor(i => i.Note).MaximumLength(300);
        });
    }
}
