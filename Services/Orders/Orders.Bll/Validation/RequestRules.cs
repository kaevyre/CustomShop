using Orders.Domain.Exceptions;

namespace Orders.Bll.Validation;

/// <summary>Спільні бізнес-перевірки (рівень 2 валідації; рівень 1 — DataAnnotations на DTO).</summary>
internal static class RequestRules
{
    public const int MaxPageSize = 100;

    public static void EnsurePaging(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
            errors["page"] = ["Page must be 1 or greater."];
        if (pageSize is < 1 or > MaxPageSize)
            errors["pageSize"] = [$"PageSize must be between 1 and {MaxPageSize}."];

        if (errors.Count > 0)
            throw new DomainValidationException(errors);
    }

    public static byte[]? ParseRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length != 8)
                throw new FormatException();
            return bytes;
        }
        catch (FormatException)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["RowVersion must be the Base64 value returned by the API (8 bytes)."]
            });
        }
    }
}
