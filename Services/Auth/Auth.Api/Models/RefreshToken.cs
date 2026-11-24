namespace Auth.Api.Models;

/// <summary>Refresh token у БД. Зберігається ХЕШ (SHA-256), сам токен знає лише клієнт (cookie).</summary>
public class RefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;

    public string TokenHash { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? CreatedByIp { get; set; }

    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    /// <summary>Rotation: хеш токена, що замінив цей.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsRevoked => RevokedAt is not null;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;
}
