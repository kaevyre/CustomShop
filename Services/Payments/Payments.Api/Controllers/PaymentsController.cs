using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Security;

namespace Payments.Api.Controllers;

public record ProcessPaymentRequest(Guid OrderId, decimal Amount);
public record RefundRequest(Guid PaymentId, string? Reason);

/// <summary>
/// Demo-сервіс платежів (порт 5003) — ендпоінти-заглушки для перевірки доступу (таблиця 3.3.3).
/// Доступ має ЛИШЕ admin: manager/user/guest отримують 403 на будь-який endpoint.
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    [HttpGet]
    [RequirePermission("payments:read")]
    public IActionResult GetAll() =>
        Ok(new[] { new { Id = Guid.Empty, OrderId = Guid.Empty, Amount = 100m, Status = "Completed" } });

    [HttpPost("process")]
    [RequirePermission("payments:process")]
    public IActionResult Process(ProcessPaymentRequest request) =>
        Ok(new { PaymentId = Guid.NewGuid(), request.OrderId, request.Amount, Status = "Processed" });

    [HttpPost("refund")]
    [RequirePermission("payments:refund")]
    public IActionResult Refund(RefundRequest request) =>
        Ok(new { request.PaymentId, Status = "Refunded", request.Reason });
}
