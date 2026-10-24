using FluentValidation;
using MongoDB.Bson;

namespace Reviews.Application.Common.Validation;

/// <summary>Спільні правила для MongoDB-специфічних полів.</summary>
public static class ValidationRules
{
    public const int MaxCustomizationKeys = 20;
    public const int MaxListItems = 10;

    public static bool IsObjectId(string? value) => ObjectId.TryParse(value, out var id) && id != ObjectId.Empty;

    /// <summary>Рядок має бути коректним 24-символьним ObjectId.</summary>
    public static IRuleBuilderOptions<T, string?> MustBeObjectId<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(IsObjectId).WithMessage("'{PropertyName}' must be a valid ObjectId (24 hex characters)");

    /// <summary>
    /// Ключі вкладеного документа (customization) стають іменами BSON-полів: без '$' на початку, без '.',
    /// не порожні; кількість і довжина обмежені, щоб документ не ріс без меж.
    /// </summary>
    public static IRuleBuilderOptions<T, IDictionary<string, string>?> MustBeValidSubdocument<T>(
        this IRuleBuilder<T, IDictionary<string, string>?> rule)
        => rule
            .Must(d => d is null || d.Count <= MaxCustomizationKeys)
            .WithMessage($"'{{PropertyName}}' must not contain more than {MaxCustomizationKeys} keys")
            .Must(d => d is null || d.Keys.All(IsValidFieldName))
            .WithMessage("'{PropertyName}' keys must be non-empty, must not start with '$' and must not contain '.'")
            .Must(d => d is null || d.Values.All(v => v is not null && v.Length <= 200))
            .WithMessage("'{PropertyName}' values must be non-null strings up to 200 characters");

    public static IRuleBuilderOptions<T, IList<string>?> MustBeShortList<T>(this IRuleBuilder<T, IList<string>?> rule,
        int maxLength = 100)
        => rule
            .Must(l => l is null || l.Count <= MaxListItems)
            .WithMessage($"'{{PropertyName}}' must not contain more than {MaxListItems} items")
            .Must(l => l is null || l.All(s => !string.IsNullOrWhiteSpace(s) && s.Length <= maxLength))
            .WithMessage($"'{{PropertyName}}' items must be non-empty and up to {maxLength} characters");

    private static bool IsValidFieldName(string key)
        => !string.IsNullOrWhiteSpace(key) && !key.StartsWith('$') && !key.Contains('.') && key.Length <= 50;
}
