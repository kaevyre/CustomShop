using Auth.Api.Data;
using Auth.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    ApplicationDbContext db) : ControllerBase
{
    private string? CurrentUserId => User.FindFirst("sub")?.Value;

    // Owner-endpoints: userId з токена збігається з {id} АБО користувач — Admin
    private bool IsOwnerOrAdmin(string id) => CurrentUserId == id || User.IsInRole("Admin");

    private static UserDto ToDto(ApplicationUser u) =>
        new(u.Id, u.UserName, u.Email, u.EmailConfirmed, u.PhoneNumber, u.FullName, u.CreatedAt);

    // GET /users — Admin
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll() =>
        Ok(await users.Users.OrderBy(u => u.UserName).Select(u => ToDto(u)).ToListAsync());

    // GET /users/{id} — Owner або Admin
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> Get(string id)
    {
        if (!IsOwnerOrAdmin(id)) return Forbid();
        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(ToDto(user));
    }

    // PUT /users/{id} — Owner або Admin
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateProfileRequest request)
    {
        if (!IsOwnerOrAdmin(id)) return Forbid();
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (request.FullName is not null) user.FullName = request.FullName;
        if (request.PhoneNumber is not null) user.PhoneNumber = request.PhoneNumber;
        var result = await users.UpdateAsync(user);

        if (result.Succeeded && request.Email is not null && !string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            result = await users.SetEmailAsync(user, request.Email); // скидає EmailConfirmed

        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(e.Code, e.Description);
            return ValidationProblem(ModelState);
        }
        return Ok(ToDto(user));
    }

    // DELETE /users/{id} — Admin
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        var result = await users.DeleteAsync(user); // refresh tokens видаляються каскадно
        return result.Succeeded ? NoContent() : Problem("Не вдалося видалити користувача");
    }

    // GET /users/{id}/refresh-tokens — Owner або Admin
    [HttpGet("{id}/refresh-tokens")]
    public async Task<ActionResult<IEnumerable<RefreshTokenDto>>> GetRefreshTokens(string id)
    {
        if (!IsOwnerOrAdmin(id)) return Forbid();
        var items = await db.RefreshTokens.Where(t => t.UserId == id)
            .OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(items.Select(t => new RefreshTokenDto(
            t.Id, t.CreatedAt, t.ExpiresAt, t.RevokedAt, t.RevokedReason, t.IsActive, t.CreatedByIp)));
    }

    // GET /users/{id}/roles — Authenticated
    [HttpGet("{id}/roles")]
    public async Task<ActionResult<IEnumerable<string>>> GetRoles(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(await users.GetRolesAsync(user));
    }

    // POST /users/{id}/roles — Admin
    [HttpPost("{id}/roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignRole(string id, AssignRoleRequest request)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!await roles.RoleExistsAsync(request.Role))
            return BadRequest(new ProblemDetails { Title = $"Роль '{request.Role}' не існує" });

        var result = await users.AddToRoleAsync(user, request.Role);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    // DELETE /users/{id}/roles/{role} — Admin
    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveRole(string id, string role)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var result = await users.RemoveFromRoleAsync(user, role);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }
}
