using Orders.Api.Middleware;
using Orders.Bll.Mapping;
using Orders.Bll.Services;
using Orders.Dal;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;
using Orders.Dal.Repositories;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
