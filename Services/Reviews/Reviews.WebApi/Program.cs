using Reviews.Application;
using Reviews.Infrastructure;
using Reviews.WebApi;
using Reviews.WebApi.Grpc;

var builder = WebApplication.CreateBuilder(args);
// Лаб. 05: Serilog (рівні — із секції "Serilog" appsettings.json), OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

// Лаб. 06: L1 — кеш у пам'яті з обмеженням розміру, L2 — Redis через Aspire-інтеграцію (ConnectionStrings:redis)
builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_000;                 // в одиницях Size записів
    o.CompactionPercentage = 0.25;
    o.TrackStatistics = true;
});
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true,
        o => { o.AbortOnConnectFail = false; o.ConnectTimeout = 2000; o.AsyncTimeout = 1000; o.SyncTimeout = 1000; });
else
    builder.Services.AddDistributedMemoryCache();   // запуск без Aspire
builder.AddCacheHealthCheck();

builder.Services.AddGrpc(o =>
{
    o.Interceptors.Add<GrpcLoggingInterceptor>();
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// кожен шар реєструє свої залежності власним методом розширення
builder.Services
    .AddApplication(builder.Configuration)       // MediatR + behaviors, валідатори, AutoMapper
    .AddInfrastructure(builder.Configuration)    // MongoDB: контекст, BsonClassMap, репозиторії, індекси, сідінг
    .AddWebApi();                                // контролери, Swagger, ProblemDetails

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();  // X-Correlation-Id → LogContext і заголовок відповіді
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapDefaultEndpoints();                     // /health і /alive (включно з перевіркою MongoDB)
app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();      // endpoint "grpc" (HTTP/2) із Kestrel:Endpoints

app.Run();

public partial class Program;
