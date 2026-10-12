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

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<CustomerDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) =>
        Ok(await _customers.ListAsync(page, pageSize, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CustomerDto>> Get(long id, CancellationToken ct) =>
        Ok(await _customers.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerDto dto, CancellationToken ct)
    {
        var created = await _customers.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<CustomerDto>> Update(long id, UpdateCustomerDto dto, CancellationToken ct) =>
        Ok(await _customers.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _customers.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/orders")]
    public async Task<ActionResult<PagedResultDto<OrderSummaryDto>>> Orders(
        long id, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default) =>
        Ok(await _orders.GetCustomerOrdersAsync(id, status, page, pageSize, ct));
}
