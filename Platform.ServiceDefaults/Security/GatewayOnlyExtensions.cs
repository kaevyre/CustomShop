using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Platform.ServiceDefaults.Security;

/// <summary>
/// «Gateway — єдина точка входу, прямий доступ до сервісів заблоковано» (Лаб. 09, п. 1.4).
/// Якщо задано конфігурацію <c>Gateway:Secret</c>, сервіс приймає запити лише із заголовком X-Gateway-Secret з цим значенням:
/// його додають Gateway (YARP transform) і Aggregator (service-to-service). Без налаштування перевірка вимкнена,
/// щоб Swagger UI сервісів і локальна розробка працювали як раніше. Ввімкнення — константа EnforceGatewayOnly в AppHost.
/// Health-ендпоінти (/health, /alive) і gRPC health дозволені завжди — на них спирається оркестратор.
/// </summary>
public static class GatewayOnlyExtensions
{
    public const string HeaderName = "X-Gateway-Secret";

    public static WebApplication UseGatewayOnly(this WebApplication app)
    {
        var secret = app.Configuration["Gateway:Secret"];
        if (string.IsNullOrEmpty(secret)) return app;

        var expected = Encoding.UTF8.GetBytes(secret);
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path;
            if (path.StartsWithSegments("/health") || path.StartsWithSegments("/alive") ||
                path.StartsWithSegments("/grpc.health.v1.Health"))
            {
                await next();
                return;
            }

            var provided = Encoding.UTF8.GetBytes(ctx.Request.Headers[HeaderName].ToString());
            if (provided.Length == expected.Length && CryptographicOperations.FixedTimeEquals(provided, expected))
            {
                await next();
                return;
            }

            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsync("Direct access is blocked: use the API Gateway.");
        });
        return app;
    }
}
