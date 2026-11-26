using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Platform.IdentityServer.Models;

namespace Platform.IdentityServer.Pages.Account;

public class LogoutModel(
    SignInManager<ApplicationUser> signInManager,
    IIdentityServerInteractionService interaction) : PageModel
{
    public string? LogoutId { get; private set; }

    public void OnGet(string? logoutId) => LogoutId = logoutId;

    public async Task<IActionResult> OnPostAsync(string? logoutId)
    {
        await signInManager.SignOutAsync();
        var context = await interaction.GetLogoutContextAsync(logoutId);
        return string.IsNullOrEmpty(context?.PostLogoutRedirectUri) ? Redirect("~/") : Redirect(context.PostLogoutRedirectUri);
    }
}
