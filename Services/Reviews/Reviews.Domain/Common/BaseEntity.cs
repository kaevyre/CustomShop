using MongoDB.Bson;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Common;

/// <summary>
/// Базовий клас кореня агрегату для MongoDB: Id — ObjectId (генерується клієнтом),
/// Version — номер версії документа для оптимістичної конкурентності.
/// </summary>
public abstract class BaseEntity
{
    public ObjectId Id { get; protected set; } = ObjectId.GenerateNewId();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }
    public int Version { get; protected set; }

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Викликає репозиторій перед збереженням зміненого документа.</summary>
    public void BumpVersion() => Version++;

    /// <summary>
    /// Рання перевірка версії, яку бачив клієнт (If-Match / поле version у запиті);
    /// остаточну атомарну перевірку робить фільтр оновлення в репозиторії.
    /// </summary>
    public void EnsureVersion(int? expectedVersion)
    {
        if (expectedVersion is { } expected && expected != Version)
            throw new ConcurrencyException(GetType().Name, Id.ToString(), expected, Version);
    }
}
