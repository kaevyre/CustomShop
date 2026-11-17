using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Caching;
using Reviews.Application.Common.Behaviors;

namespace Reviews.Application;

public static class DependencyInjection
{
    /// <summary>
    /// MediatR (обробники зі сканування збірки + pipeline behaviors), валідатори FluentValidation, профілі AutoMapper.
    /// Порядок AddOpenBehavior = порядок виконання: Logging → UnhandledException → Validation → Performance → обробник.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));             // 1 — зовнішній
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));  // 2
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));          // 3 — до обробника не пропускає невалідне
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));             // 4 — кешує лише валідні запити (Лаб. 06)
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));         // 5 — біля обробника
        });

        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<ITwoLevelCache, TwoLevelCache>();
        services.AddSingleton<ReviewsCacheInvalidator>();

        services.AddValidatorsFromAssembly(assembly);
        // стандартні повідомлення FluentValidation — англійською (інакше мова залежить від ОС)
        ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("en");

        services.AddAutoMapper(assembly);
        return services;
    }
}
