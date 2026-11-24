using Auth.Api.Models;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Auth.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IJwtTokenService jwt,
    IRefreshTokenService refreshTokens,
    IEmailService email,
    IOptions<JwtSettings> jwtOptions) : ControllerBase
{
    private const string RefreshCookie = "refreshToken";
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ───────── POST /auth/register (Anonymous)
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Username, Email = request.Email };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return IdentityProblem(created);

        await users.AddToRoleAsync(user, "User"); // роль User — автоматично

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        await email.SendEmailConfirmationAsync(user, token);

        return StatusCode(StatusCodes.Status201Created,
            new { user.Id, user.UserName, user.Email, Message = "Перевірте пошту: підтвердіть email, щоб увійти." });
    }

    // ───────── POST /auth/login (Anonymous) → access у тілі, refresh — у HttpOnly cookie
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await users.FindByNameAsync(request.UsernameOrEmail)
                   ?? await users.FindByEmailAsync(request.UsernameOrEmail);
        if (user is null) return Unauthorized(new ProblemDetails { Title = "Невірні облікові дані" });

        if (await users.IsLockedOutAsync(user))
            return StatusCode(StatusCodes.Status423Locked, new ProblemDetails { Title = "Обліковий запис тимчасово заблоковано" });

        // lockoutOnFailure: після 5 невдалих спроб — lockout
        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked, new ProblemDetails { Title = "Обліковий запис тимчасово заблоковано" });
        if (!check.Succeeded) return Unauthorized(new ProblemDetails { Title = "Невірні облікові дані" });

        // непідтверджені користувачі (EmailConfirmed = false) не можуть увійти
        if (!user.EmailConfirmed)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Email не підтверджено" });

        return await IssueTokensAsync(user);
    }

    // ───────── POST /auth/refresh-token (Anonymous) — rotation
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var raw) || string.IsNullOrEmpty(raw))
            return Unauthorized(new ProblemDetails { Title = "Refresh token відсутній" });

        var rotation = await refreshTokens.RotateAsync(raw, Ip);
        if (!rotation.Success)
        {
            Response.Cookies.Delete(RefreshCookie, BuildCookie(DateTimeOffset.UtcNow));
            return Unauthorized(new ProblemDetails { Title = "Недійсний refresh token" });
        }

        var user = await users.FindByIdAsync(rotation.UserId!);
        if (user is null) return Unauthorized();

        SetRefreshCookie(rotation.NewRawToken!, rotation.ExpiresAt!.Value);
        return Ok(await BuildAuthResponseAsync(user));
    }

    // ───────── POST /auth/revoke-token (Authenticated)
    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<IActionResult> Revoke(RevokeTokenRequest? request)
    {
        var raw = request?.Token ?? Request.Cookies[RefreshCookie];
        if (string.IsNullOrEmpty(raw)) return BadRequest(new ProblemDetails { Title = "Токен не вказано" });

        var token = await refreshTokens.FindAsync(raw);
        if (token is null) return NotFound();

        // відкликати можна лише власний токен (або будь-який — Admin)
        if (token.UserId != CurrentUserId && !User.IsInRole("Admin")) return Forbid();

        await refreshTokens.RevokeAsync(token, "Revoked by user");
        return NoContent();
    }

    // ───────── POST /auth/logout (Authenticated)
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var raw) && !string.IsNullOrEmpty(raw))
        {
            var token = await refreshTokens.FindAsync(raw);
            if (token is not null && token.UserId == CurrentUserId)
                await refreshTokens.RevokeAsync(token, "Logout");
        }
        Response.Cookies.Delete(RefreshCookie, BuildCookie(DateTimeOffset.UtcNow));
        return NoContent();
    }

    // ───────── POST /auth/verify-email (Anonymous) — токен одноразовий (ConfirmEmailAsync змінює SecurityStamp)
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null) return BadRequest(new ProblemDetails { Title = "Недійсне посилання підтвердження" });

        var result = await users.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded ? Ok(new { Message = "Email підтверджено" }) : IdentityProblem(result);
    }

    // ───────── POST /auth/resend-verification (Anonymous) — завжди 200, щоб не розкривати існування акаунтів
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification(EmailRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null && !user.EmailConfirmed)
            await email.SendEmailConfirmationAsync(user, await users.GenerateEmailConfirmationTokenAsync(user));

        return Ok(new { Message = "Якщо акаунт існує і не підтверджений — лист надіслано." });
    }

    // ───────── POST /auth/forgot-password (Anonymous)
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(EmailRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null && user.EmailConfirmed)
            await email.SendPasswordResetAsync(user, await users.GeneratePasswordResetTokenAsync(user));

        return Ok(new { Message = "Якщо акаунт існує — лист зі скиданням пароля надіслано." });
    }

    // ───────── POST /auth/reset-password (Anonymous) — після скидання відкликаємо ВСІ refresh tokens
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return BadRequest(new ProblemDetails { Title = "Недійсний токен скидання пароля" });

        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded) return IdentityProblem(result);

        await refreshTokens.RevokeAllAsync(user.Id, "Password reset");
        return Ok(new { Message = "Пароль скинуто" });
    }

    // ───────── POST /auth/change-password (Authenticated) — з перевіркою поточного пароля
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await users.FindByIdAsync(CurrentUserId!);
        if (user is null) return Unauthorized();

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return IdentityProblem(result);

        await refreshTokens.RevokeAllAsync(user.Id, "Password changed");
        return Ok(new { Message = "Пароль змінено" });
    }

    // ───────── helpers
    private string? CurrentUserId => User.FindFirst("sub")?.Value;

    private async Task<IActionResult> IssueTokensAsync(ApplicationUser user)
    {
        var (raw, entity) = await refreshTokens.IssueAsync(user.Id, Ip);
        SetRefreshCookie(raw, entity.ExpiresAt);
        return Ok(await BuildAuthResponseAsync(user));
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var access = jwt.GenerateAccessToken(user, roles);
        return new AuthResponse(access, "Bearer", jwtOptions.Value.AccessTokenMinutes * 60);
    }

    // refresh token — лише в HttpOnly cookie (JS на сторінці його не бачить), access — у тілі відповіді
    private void SetRefreshCookie(string raw, DateTime expiresUtc) =>
        Response.Cookies.Append(RefreshCookie, raw, BuildCookie(new DateTimeOffset(expiresUtc, TimeSpan.Zero)));

    private CookieOptions BuildCookie(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Expires = expires,
        Path = "/auth"
    };

    private IActionResult IdentityProblem(IdentityResult result)
    {
        foreach (var e in result.Errors) ModelState.AddModelError(e.Code, e.Description);
        return ValidationProblem(ModelState);
    }
}
