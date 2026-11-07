using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// Централізоване логування винятків на рівні MediatR: очікувані (валідація, домен, 404/409) — Warning,
/// решта — Error з повним стеком. Виняток прокидається далі: HTTP-статус визначає WebApi.
/// </summary>
public sealed class UnhandledExceptionBehavior<TRequest, TResponse>(
    ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) when (ex is ValidationException or DomainException or NotFoundException or ConflictException)
        {
            logger.LogWarning("{RequestName} rejected: {ExceptionType} {Reason}",
                typeof(TRequest).Name, ex.GetType().Name, ex.Message);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{RequestName} failed {@Request}", typeof(TRequest).Name, request);
            throw;
        }
    }
}
