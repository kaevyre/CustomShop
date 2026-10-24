using FluentValidation;
using MediatR;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// Запускає всі валідатори FluentValidation для команди/запиту (включно з асинхронними правилами,
/// що звертаються до MongoDB), збирає помилки разом і кидає ValidationException — обробник не викликається.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
