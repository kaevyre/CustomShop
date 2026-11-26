using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Models;

public record RegisterRequest(
    [Required, MinLength(3)] string Username,
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginRequest([Required] string UsernameOrEmail, [Required] string Password);

public record RevokeTokenRequest(string? Token);

public record VerifyEmailRequest([Required] string UserId, [Required] string Token);

public record EmailRequest([Required, EmailAddress] string Email);

public record ResetPasswordRequest([Required, EmailAddress] string Email, [Required] string Token, [Required] string NewPassword);

public record ChangePasswordRequest([Required] string CurrentPassword, [Required] string NewPassword);

public record UpdateProfileRequest(string? Email, string? PhoneNumber, string? FullName);

public record AssignRoleRequest([Required] string Role);

public record AuthResponse(string AccessToken, string TokenType, int ExpiresInSeconds);

public record UserDto(string Id, string? UserName, string? Email, bool EmailConfirmed, string? PhoneNumber, string? FullName, DateTime CreatedAt);

public record RefreshTokenDto(int Id, DateTime CreatedAt, DateTime ExpiresAt, DateTime? RevokedAt, string? RevokedReason, bool IsActive, string? CreatedByIp);
