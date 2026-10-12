namespace Orders.Domain.Models;

public abstract class BaseEntity
{
    public long Id { get; set; }
}

/// <summary>Сутність з оптимістичною конкурентністю (колонка ROWVERSION).</summary>
public interface IVersioned
{
    byte[]? RowVersion { get; set; }
}

public abstract class AuditableEntity : BaseEntity, IVersioned
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public byte[]? RowVersion { get; set; }
}
