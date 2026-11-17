using Orders.Api.Grpc;
using Orders.Api.Middleware;
using Orders.Bll.Caching;
using Orders.Bll.Mapping;
using Orders.Bll.Services;
using Orders.Dal;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;
using Orders.Dal.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();                 // Лаб. 05: Serilog, OpenTelemetry, health checks, service discovery

// Рядок підключення: appsettings.json або змінна середовища ConnectionStrings__OrdersDb (її передає Aspire через WithReference)
var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is not configured.");

// DAL: усе Scoped — одне з'єднання та один Unit of Work на HTTP-запит
builder.Services.AddScoped<IDbSession>(_ => new DbSession(connectionString));
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();
builder.Services.AddScoped<IOrderSummaryRepository, OrderSummaryRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// BLL: AutoMapper-профілі та сервіси
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();

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
builder.Services.AddSingleton<CacheMetrics>();
builder.Services.AddSingleton<ITwoLevelCache, TwoLevelCache>();
builder.AddCacheHealthCheck();

builder.Services.AddGrpc(o =>
{
    o.Interceptors.Add<GrpcLoggingInterceptor>();
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();  // X-Correlation-Id → LogContext і заголовок відповіді
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapDefaultEndpoints();                     // /health і /alive
app.MapControllers();
app.MapGrpcService<OrdersGrpcService>();       // endpoint "grpc" (HTTP/2) із Kestrel:Endpoints

app.Run();
