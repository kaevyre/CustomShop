using System.Reflection;
using System.Text.Json.Serialization;
using Reviews.WebApi.Infrastructure;
using Reviews.WebApi.Mapping;

namespace Reviews.WebApi;

public static class DependencyInjection
{
    /// <summary>Контролери, Swagger, ProblemDetails + глобальний обробник винятків, профіль AutoMapper HTTP-контрактів.</summary>
    public static IServiceCollection AddWebApi(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "Reviews API (Clean Architecture + CQRS + MongoDB)", Version = "v1" });
            var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xml))
                options.IncludeXmlComments(xml);
        });

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // AutoMapper: профіль Application (сутності → DTO) реєструє AddApplication, тут — HTTP-контракти → команди
        services.AddAutoMapper(typeof(ApiMappingProfile).Assembly);
        return services;
    }
}
