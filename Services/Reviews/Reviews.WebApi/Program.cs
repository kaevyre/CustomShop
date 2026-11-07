using Reviews.Application;
using Reviews.Infrastructure;
using Reviews.WebApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog: рівні — у секції "Serilog" appsettings.json; структуровані властивості ({@Request}) виводяться в {Properties}
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "reviews-api")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj} {Properties:j}{NewLine}{Exception}"));

// кожен шар реєструє свої залежності власним методом розширення
builder.Services
    .AddApplication(builder.Configuration)       // MediatR + behaviors, валідатори, AutoMapper
    .AddInfrastructure(builder.Configuration)    // MongoDB: контекст, BsonClassMap, репозиторії, індекси, сідінг
    .AddWebApi();                                // контролери, Swagger, ProblemDetails

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;
