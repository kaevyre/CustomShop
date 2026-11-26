using Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;
    private readonly IOrderService _orders;

    public CustomersController(ICustomerService customers, IOrderService orders)
    {
        _customers = customers;
        _orders = orders;
    }

    [RequirePermission("orders:read")]
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<CustomerDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) =>
        Ok(await _customers.ListAsync(page, pageSize, ct));

    [RequirePermission("orders:read")]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<CustomerDto>> Get(long id, CancellationToken ct) =>
        Ok(await _customers.GetAsync(id, ct));

    [RequirePermission("orders:create")]
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerDto dto, CancellationToken ct)
    {
        var created = await _customers.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [RequirePermission("orders:update")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<CustomerDto>> Update(long id, UpdateCustomerDto dto, CancellationToken ct) =>
        Ok(await _customers.UpdateAsync(id, dto, ct));

    [RequirePermission("orders:delete")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _customers.DeleteAsync(id, ct);
        return NoContent();
    }

    [RequirePermission("orders:read")]
    [HttpGet("{id:long}/orders")]
    public async Task<ActionResult<PagedResultDto<OrderSummaryDto>>> Orders(
        long id, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default) =>
        Ok(await _orders.GetCustomerOrdersAsync(id, status, page, pageSize, ct));
}
