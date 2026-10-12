namespace Orders.Domain.Exceptions;

/// <summary>
/// Базовий тип доменних винятків. Глобальний обробник в API перетворює їх на ProblemDetails:
/// NotFound → 404, BusinessConflict → 409, Validation → 400.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Запитаної сутності не існує (HTTP 404).</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public NotFoundException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public string? EntityName { get; }
    public object? Key { get; }
}

/// <summary>
/// Порушено бізнес-правило: недопустимий перехід статусу, недостатньо товару на складі,
/// дубль унікального значення, конфлікт версій (HTTP 409).
/// </summary>
public sealed class BusinessConflictException : DomainException
{
    public BusinessConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public DomainValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }
}
