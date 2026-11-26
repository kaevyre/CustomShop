using Auth.Api.Models;

namespace Auth.Api.Services;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(ApplicationUser user, string token);
    Task SendPasswordResetAsync(ApplicationUser user, string token);
}

/// <summary>Mock email service для development: «лист» пишеться в лог, з нього копіюється токен для Postman.</summary>
public sealed class MockEmailService(ILogger<MockEmailService> logger) : IEmailService
{
    public Task SendEmailConfirmationAsync(ApplicationUser user, string token)
    {
        logger.LogInformation(
            "[MOCK EMAIL] To: {Email} | Підтвердіть email: POST /auth/verify-email {{ \"userId\": \"{UserId}\", \"token\": \"{Token}\" }}",
            user.Email, user.Id, token);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(ApplicationUser user, string token)
    {
        logger.LogInformation(
            "[MOCK EMAIL] To: {Email} | Скидання пароля: POST /auth/reset-password {{ \"email\": \"{Email}\", \"token\": \"{Token}\", \"newPassword\": \"...\" }}",
            user.Email, user.Email, token);
        return Task.CompletedTask;
    }
}
