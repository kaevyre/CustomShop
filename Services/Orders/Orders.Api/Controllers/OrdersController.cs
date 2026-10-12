using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDto>> Get(long id, CancellationToken ct) =>
        Ok(await _orders.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Place(CreateOrderDto dto, CancellationToken ct)
    {
        var order = await _orders.PlaceAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPost("{id:long}/status")]
    public async Task<ActionResult<OrderStatusResultDto>> ChangeStatus(
        long id, ChangeOrderStatusDto dto, CancellationToken ct) =>
        Ok(await _orders.ChangeStatusAsync(id, dto, ct));

    [HttpGet("statistics")]
    public async Task<ActionResult<IReadOnlyList<OrderStatusStatisticsDto>>> Statistics(CancellationToken ct) =>
        Ok(await _orders.GetStatusStatisticsAsync(ct));
}
