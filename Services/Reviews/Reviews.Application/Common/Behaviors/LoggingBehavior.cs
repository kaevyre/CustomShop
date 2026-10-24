using MediatR;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// Зовнішній behavior: структурований лог кожної команди/запиту та її відповіді.
/// Якщо запит відхилено валідацією, «Handled» у лозі не з'являється — обробник не викликався.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName} {@Request}", requestName, request);

        var response = await next();

        logger.LogInformation("Handled {RequestName} -> {ResponseType} {@Response}",
            requestName, typeof(TResponse).Name, Summarize(response));
        return response;
    }

    // Великі відповіді (сторінки, повні документи) не пишемо в лог повністю.
    private static object? Summarize(TResponse response) => response switch
    {
        null or Unit => null,
        string or ValueType => response,
        _ => typeof(TResponse).Name
    };
}
