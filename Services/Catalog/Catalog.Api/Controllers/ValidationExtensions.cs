using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

internal static class ValidationExtensions
{
    /// <summary>
    /// Явний виклик валідатора FluentValidation (автоматична інтеграція AddFluentValidation застаріла).
    /// Повертає 400 ValidationProblemDetails (той самий формат, що й для DataAnnotations) або null, якщо дані коректні.
    /// </summary>
    public static async Task<ActionResult?> ValidateAsync<T>(
        this ControllerBase controller, IValidator<T> validator, T model, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        if (result.IsValid)
            return null;

        foreach (var error in result.Errors)
            controller.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

        return controller.ValidationProblem(controller.ModelState);
    }
}
