using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Reviews.WebApi.Controllers;

/// <summary>
/// Базовий контролер: лише ISender (MediatR) і IMapper — жодних репозиторіїв чи IMongoCollection.
/// Плюс робота з ETag / If-Match для оптимістичної конкурентності.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    private IMapper? _mapper;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
    protected IMapper Mapper => _mapper ??= HttpContext.RequestServices.GetRequiredService<IMapper>();

    /// <summary>ETag = версія документа: GET повертає її, PUT/DELETE передають назад в If-Match.</summary>
    protected void SetETag(int version) => Response.Headers.ETag = $"\"{version}\"";

    /// <summary>
    /// Очікувана версія з If-Match ("3" або W/"3"); якщо заголовка немає — з тіла запиту (fallback).
    /// </summary>
    protected int? ExpectedVersion(int? fromBody = null)
    {
        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
            return fromBody;

        var raw = ifMatch.Trim();
        if (raw.StartsWith("W/", StringComparison.Ordinal))
            raw = raw[2..];
        raw = raw.Trim('"');

        if (int.TryParse(raw, out var version) && version >= 0)
            return version;

        throw new ValidationException([new ValidationFailure("If-Match",
            "If-Match must contain a document version from ETag, e.g. \"3\"")]);
    }
}
