using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
public sealed class AggregatorController(OrderCardService cards, HttpOrderCardService httpCards) : ControllerBase
{
    /// <summary>
    /// Повна картка замовлення: замовлення (orders-api) + актуальні товари (catalog-api) + відгуки (reviews-api).
    /// 200 — і тоді, коли catalog-api чи reviews-api недоступні (дані частково, причина у warnings);
    /// 404 — замовлення немає; 503 — недоступний orders-api (без замовлення агрегувати нічого).
    /// CancellationToken = HttpContext.RequestAborted: якщо клієнт скасував запит, зупиняються всі вихідні виклики.
    /// </summary>
    [HttpGet("orders/{id:long}/full")]
    [ProducesResponseType<FullOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullOrderDto>> GetFullOrder(long id, [FromQuery] bool noCache, CancellationToken ct) =>
        ToActionResult(id, await cards.GetFullOrderAsync(id, ct, useCache: !noCache));

    /// <summary>Та сама картка через HTTP REST (Лаб. 05), без кешу: база для порівняння затримок із gRPC.</summary>
    [HttpGet("orders/{id:long}/full/http")]
    [ProducesResponseType<FullOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullOrderDto>> GetFullOrderOverHttp(long id, CancellationToken ct) =>
        ToActionResult(id, await httpCards.GetFullOrderAsync(id, ct));

    private ActionResult<FullOrderDto> ToActionResult(long id, OrderCardResult result) => result.Outcome switch
    {
        OrderCardOutcome.Found => Ok(result.Card),
        OrderCardOutcome.NotFound => Problem(statusCode: StatusCodes.Status404NotFound,
            title: "Order not found", detail: $"Order {id} does not exist."),
        _ => Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "orders-api is unavailable", detail: "The order card cannot be built without the order itself.")
    };
}
