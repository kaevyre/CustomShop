using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class Brand : AuditableEntity
{
    private Brand()
    {
    }

    public Brand(string name, string? country = null, string? website = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Brand name is required.");
        if (website is not null && !website.StartsWith("https://", StringComparison.Ordinal))
            throw new DomainException("Brand website must start with https://.");

        Name = name.Trim();
        Country = country?.Trim();
        Website = website;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Country { get; private set; }
    public string? Website { get; private set; }
}
