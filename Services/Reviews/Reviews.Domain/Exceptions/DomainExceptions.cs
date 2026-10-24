namespace Reviews.Domain.Exceptions;

/// <summary>Порушення бізнес-правила домену (→ 400).</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Документ не знайдено (→ 404).</summary>
public class NotFoundException(string entity, string id)
    : Exception($"{entity} '{id}' was not found");

/// <summary>Конфлікт стану (→ 409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Документ змінено іншим запитом після того, як клієнт його прочитав (→ 409).</summary>
public sealed class ConcurrencyException(string entity, string id, int expected, int actual)
    : ConflictException($"{entity} '{id}' was modified: expected version {expected}, current version {actual}");
