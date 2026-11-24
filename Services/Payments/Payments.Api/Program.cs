using Platform.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();                 // Лаб. 05: Serilog, OpenTelemetry, health checks, service discovery

// Лаб. 09: JWT валідується САМОСТІЙНО (Auth Service / IdentityServer / Keycloak — за issuer токена), без запитів до провайдера.
// Keycloak-audience = "payments-api". Payments — найчутливіший ресурс: permissions видає ЛИШЕ роль admin.
builder.AddPlatformAuthentication(new ServiceAudiences(Keycloak: "payments-api", IdentityServer: "payments_api"));

// Політика на КОЖЕН permission з таблиці 3.3.3 (Payments Service)
builder.Services.AddPermissionAuthorization("payments:read", "payments:process", "payments:refund");

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseGatewayOnly();                  // Лаб. 09: прямий доступ повз Gateway блокується (якщо задано Gateway:Secret)
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
