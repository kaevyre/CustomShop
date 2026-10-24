using Catalog.Bll.Dtos;
using Catalog.Domain.Entities;
using FluentValidation;

namespace Catalog.Bll.Validation;

// Прості правила (обов'язковість, довжини, діапазони) — атрибути DataAnnotations на DTO.
// Тут — формати, допустимі значення й перевірки, що стосуються кількох полів.

public sealed class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Sku).Matches("^[A-Z0-9-]+$")
            .WithMessage("SKU may contain only uppercase letters, digits and '-'.");
        RuleFor(x => x.Slug).Must(CatalogRules.IsValidSlug)
            .WithMessage("Slug may contain only a-z, 0-9 and '-'.");
        RuleFor(x => x.ProductType).Must(t => CatalogRules.ProductTypes.Contains(t))
            .WithMessage($"ProductType must be one of: {string.Join(", ", CatalogRules.ProductTypes)}.");
        RuleFor(x => x.BasePrice).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
    }
}

public sealed class ChangePriceDtoValidator : AbstractValidator<ChangePriceDto>
{
    public ChangePriceDtoValidator()
    {
        RuleFor(x => x.Price).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.RowVersion).Must(BeRowVersion!)
            .When(x => !string.IsNullOrEmpty(x.RowVersion))
            .WithMessage("RowVersion must be the Base64 value returned by the API (8 bytes).");
    }

    private static bool BeRowVersion(string value)
    {
        var buffer = new byte[16];
        return Convert.TryFromBase64String(value, buffer, out var written) && written == 8;
    }
}

public sealed class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator()
    {
        RuleFor(x => x.Slug).Must(CatalogRules.IsValidSlug)
            .WithMessage("Slug may contain only a-z, 0-9 and '-'.");
    }
}

public sealed class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    public ProductQueryValidator()
    {
        RuleFor(x => x.SortBy).Must(s => ProductQuery.SortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", ProductQuery.SortFields)}.");
        RuleFor(x => x.SortDir).Must(d => d is "asc" or "desc" or "ASC" or "DESC")
            .WithMessage("SortDir must be 'asc' or 'desc'.");
        RuleFor(x => x.ProductType).Must(t => CatalogRules.ProductTypes.Contains(t!))
            .When(x => !string.IsNullOrEmpty(x.ProductType))
            .WithMessage($"ProductType must be one of: {string.Join(", ", CatalogRules.ProductTypes)}.");
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice is not null);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .When(x => x.MinPrice is not null && x.MaxPrice is not null)
            .WithMessage("MaxPrice must be greater than or equal to MinPrice.");
    }
}
