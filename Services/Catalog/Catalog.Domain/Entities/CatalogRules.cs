using System.Text.RegularExpressions;

namespace Catalog.Domain.Entities;

/// <summary>Допустимі значення й формати — ті самі, що в CHECK-обмеженнях БД.</summary>
public static partial class CatalogRules
{
    public static readonly IReadOnlyList<string> ProductTypes =
        ["Keyboard", "Mouse", "Headphones", "Speakers", "Mousepad", "Accessory"];

    public static readonly IReadOnlyList<string> ConnectionTypes =
        ["Wired", "Wireless", "Bluetooth", "Hybrid", "None"];

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex SlugPattern();

    public static bool IsValidSlug(string? slug) => !string.IsNullOrEmpty(slug) && SlugPattern().IsMatch(slug);
}
