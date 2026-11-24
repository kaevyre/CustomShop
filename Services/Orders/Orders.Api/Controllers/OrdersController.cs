using Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Mvc;
using Orders.Api.Metrics;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Orders.Domain.Models;

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

    [RequirePermission("orders:read")]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDto>> Get(long id, CancellationToken ct) =>
        Ok(await _metrics.MeasureAsync("get", () => _orders.GetAsync(id, ct)));

    [RequirePermission("orders:create")]
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Place(CreateOrderDto dto, CancellationToken ct)
    {
        var order = await _metrics.MeasureAsync("place", () => _orders.PlaceAsync(dto, ct));
        _metrics.OrderCreated(order.Status);          // лічильник — лише після успішного створення
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [RequirePermission("orders:update")]
    [HttpPost("{id:long}/status")]
    public async Task<ActionResult<OrderStatusResultDto>> ChangeStatus(
        long id, ChangeOrderStatusDto dto, CancellationToken ct)
    {
        var result = await _metrics.MeasureAsync("change_status", () => _orders.ChangeStatusAsync(id, dto, ct));
        _metrics.StatusChanged(result.Status);
        return Ok(result);
    }

    /// <summary>PUT /api/orders/{id}: оновлення замовлення (зміна статусу за правилами переходів) — permission orders:update.</summary>
    [RequirePermission("orders:update")]
    [HttpPut("{id:long}")]
    public Task<ActionResult<OrderStatusResultDto>> Update(long id, ChangeOrderStatusDto dto, CancellationToken ct) =>
        ChangeStatus(id, dto, ct);

    /// <summary>Підтвердження замовлення менеджером: Submitted → AwaitingValidation — permission orders:approve.</summary>
    [RequirePermission("orders:approve")]
    [HttpPost("{id:long}/approve")]
    public async Task<ActionResult<OrderStatusResultDto>> Approve(long id, CancellationToken ct)
    {
        var dto = new ChangeOrderStatusDto { NewStatus = OrderStatus.AwaitingValidation };
        var result = await _metrics.MeasureAsync("approve", () => _orders.ChangeStatusAsync(id, dto, ct));
        _metrics.StatusChanged(result.Status);
        return Ok(result);
    }

    /// <summary>DELETE /api/orders/{id}: скасування замовлення (Cancelled; фізично запис не видаляється) — permission orders:delete.</summary>
    [RequirePermission("orders:delete")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var dto = new ChangeOrderStatusDto { NewStatus = OrderStatus.Cancelled };
        var result = await _metrics.MeasureAsync("cancel", () => _orders.ChangeStatusAsync(id, dto, ct));
        _metrics.StatusChanged(result.Status);
        return NoContent();
    }

    [RequirePermission("orders:read")]
    [HttpGet("statistics")]
    public async Task<ActionResult<IReadOnlyList<OrderStatusStatisticsDto>>> Statistics(CancellationToken ct) =>
        Ok(await _metrics.MeasureAsync("statistics", () => _orders.GetStatusStatisticsAsync(ct)));
}
