using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Platform.IdentityServer.Models;

namespace Platform.IdentityServer.Pages.Account;

/// <summary>Сторінка логіну для Authorization Code Flow (мінімальний аналог Quickstart UI).</summary>
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    IIdentityServerInteractionService interaction) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; private set; }

    public class InputModel
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public void OnGet(string? returnUrl) => Input.ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync()
    {
        // returnUrl обов'язково повинен бути валідним authorization request, інакше — open redirect
        var context = await interaction.GetAuthorizationContextAsync(Input.ReturnUrl);
        if (context is null) return Redirect("~/");

        var result = await signInManager.PasswordSignInAsync(
            Input.Username ?? "", Input.Password ?? "", isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded) return Redirect(Input.ReturnUrl!);

        Error = result.IsLockedOut ? "Обліковий запис заблоковано" : "Невірний логін або пароль";
        return Page();
    }
}
