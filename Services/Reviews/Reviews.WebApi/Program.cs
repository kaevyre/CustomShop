using Reviews.Application;
using Reviews.Infrastructure;
using Reviews.WebApi;

var builder = WebApplication.CreateBuilder(args);
// Лаб. 05: Serilog (рівні — із секції "Serilog" appsettings.json), OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

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

app.Run();

public partial class Program;
