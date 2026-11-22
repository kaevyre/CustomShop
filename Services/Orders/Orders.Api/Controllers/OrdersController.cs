using Microsoft.AspNetCore.Mvc;
using Orders.Api.Metrics;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly OrderMetrics _metrics;

    public OrdersController(IOrderService orders, OrderMetrics metrics)
    {
        _orders = orders;
        _metrics = metrics;
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDto>> Get(long id, CancellationToken ct) =>
        Ok(await _metrics.MeasureAsync("get", () => _orders.GetAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Place(CreateOrderDto dto, CancellationToken ct)
    {
        var order = await _metrics.MeasureAsync("place", () => _orders.PlaceAsync(dto, ct));
        _metrics.OrderCreated(order.Status);          // лічильник — лише після успішного створення
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPost("{id:long}/status")]
    public async Task<ActionResult<OrderStatusResultDto>> ChangeStatus(
        long id, ChangeOrderStatusDto dto, CancellationToken ct)
    {
        var result = await _metrics.MeasureAsync("change_status", () => _orders.ChangeStatusAsync(id, dto, ct));
        _metrics.StatusChanged(result.Status);
        return Ok(result);
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<IReadOnlyList<OrderStatusStatisticsDto>>> Statistics(CancellationToken ct) =>
        Ok(await _metrics.MeasureAsync("statistics", () => _orders.GetStatusStatisticsAsync(ct)));
}
