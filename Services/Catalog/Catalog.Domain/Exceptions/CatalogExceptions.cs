namespace Catalog.Domain.Exceptions;

/// <summary>Порушено бізнес-правило самої сутності (ціна ≤ 0, недостатній залишок) → 409.</summary>
public class DomainException : Exception
{
    public DomainException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Запитаної сутності не існує → 404.</summary>
public sealed class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }
    public object Key { get; }
}

/// <summary>Конфлікт зі станом даних: дубль SKU, застарілий RowVersion, порушення обмеження БД → 409.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Дані запиту не відповідають бізнес-вимогам → 400 з переліком помилок за полями.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string field, string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]> { [field] = [message] };
    }

    public IDictionary<string, string[]> Errors { get; }
}
