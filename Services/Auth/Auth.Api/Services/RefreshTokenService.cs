using Auth.Api.Data;
using Auth.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.Api.Services;

public record RotationResult(bool Success, string? UserId = null, string? NewRawToken = null, DateTime? ExpiresAt = null);

public interface IRefreshTokenService
{
    Task<(string Raw, RefreshToken Entity)> IssueAsync(string userId, string? ip, CancellationToken ct = default);
    Task<RotationResult> RotateAsync(string rawToken, string? ip, CancellationToken ct = default);
    Task<RefreshToken?> FindAsync(string rawToken, CancellationToken ct = default);
    Task RevokeAsync(RefreshToken token, string reason, CancellationToken ct = default);
    Task RevokeAllAsync(string userId, string reason, CancellationToken ct = default);
}

public sealed class RefreshTokenService(
    ApplicationDbContext db, IJwtTokenService jwt, IOptions<JwtSettings> options) : IRefreshTokenService
{
    public async Task<(string Raw, RefreshToken Entity)> IssueAsync(string userId, string? ip, CancellationToken ct = default)
    {
        var raw = jwt.GenerateRefreshToken();
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = TokenHasher.Hash(raw),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(options.Value.RefreshTokenDays), // 7 днів
            CreatedByIp = ip
        };
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return (raw, entity);
    }

    /// <summary>Rotation: старий токен відкликається, створюється новий. Повторне використання відкликаного → відкликаємо всі токени користувача.</summary>
    public async Task<RotationResult> RotateAsync(string rawToken, string? ip, CancellationToken ct = default)
    {
        var existing = await FindAsync(rawToken, ct);
        if (existing is null) return new RotationResult(false);

        if (existing.IsRevoked)
        {
            await RevokeAllAsync(existing.UserId, "Attempted reuse of a revoked refresh token", ct);
            return new RotationResult(false);
        }
        if (existing.IsExpired) return new RotationResult(false);

        var (newRaw, newEntity) = await IssueAsync(existing.UserId, ip, ct);
        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedReason = "Replaced by new token (rotation)";
        existing.ReplacedByTokenHash = newEntity.TokenHash;
        await db.SaveChangesAsync(ct);

        return new RotationResult(true, existing.UserId, newRaw, newEntity.ExpiresAt);
    }

    public Task<RefreshToken?> FindAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = TokenHasher.Hash(rawToken);
        return db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
    }

    public async Task RevokeAsync(RefreshToken token, string reason, CancellationToken ct = default)
    {
        if (token.IsRevoked) return;
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedReason = reason;
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllAsync(string userId, string reason, CancellationToken ct = default)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active)
        {
            t.RevokedAt = DateTime.UtcNow;
            t.RevokedReason = reason;
        }
        await db.SaveChangesAsync(ct);
    }
}
