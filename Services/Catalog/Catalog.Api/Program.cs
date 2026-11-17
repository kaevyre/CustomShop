using System.Globalization;
using Catalog.Api.Grpc;
using Catalog.Api.Middleware;
using Catalog.Bll.Caching;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Bll.Validation;
using Catalog.Dal;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Seeding;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
// Лаб. 05: Serilog (рівні — як і раніше, із секції "Serilog" appsettings.json), OpenTelemetry, health checks
builder.AddServiceDefaults();

// Рядок підключення: appsettings.json або змінна середовища ConnectionStrings__CatalogDb (її передає Aspire)
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is not configured.");

builder.Services.AddDbContext<CatalogDbContext>(options => options
    .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
    // фільтр м'якого видалення на Product навмисно приховує й пов'язані записи видаленого товару
    .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddAutoMapper(typeof(CatalogProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductDtoValidator>();
// стандартні повідомлення FluentValidation — англійською, як і решта відповідей API (інакше мова залежить від ОС)
ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("en");
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IBrandService, BrandService>();

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
builder.Services.AddHostedService<CacheWarmupService>();
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
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Міграції й сідінг. Сідінг вимикається в production або змінною Database__Seed=false.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue("Database:Seed", !app.Environment.IsProduction()))
        await DbInitializer.SeedAsync(db, app.Logger);
}

app.UseMiddleware<CorrelationIdMiddleware>();  // спільний middleware з ServiceDefaults (межі запиту + CorrelationId)
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapDefaultEndpoints();                     // /health і /alive
app.MapControllers();
app.MapGrpcService<CatalogGrpcService>();      // endpoint "grpc" (HTTP/2) із Kestrel:Endpoints

app.Run();
