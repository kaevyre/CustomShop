namespace Platform.Aggregator.Resilience;

/// <summary>
/// Параметри resilience одного downstream-сервісу. Значення підібрані за критичністю сервісу для картки замовлення
/// (обґрунтування кожного — у таблиці README.md, розділ «Лаб. 07»).
/// </summary>
/// <param name="Service">Назва сервісу для логів і документації.</param>
/// <param name="MaxRetryAttempts">Кількість повторів (поверх першої спроби).</param>
/// <param name="RetryDelay">Базова затримка (експоненційна, з jitter).</param>
/// <param name="AttemptTimeout">Таймаут однієї спроби.</param>
/// <param name="TotalTimeout">Загальний таймаут з усіма повторами: приблизно AttemptTimeout × (повтори + 1) + затримки.</param>
/// <param name="FailureRatio">Частка збоїв у вікні, що розмикає circuit breaker.</param>
/// <param name="MinimumThroughput">Мінімум запитів у вікні, щоб рахувати частку (навчальний стенд — малий трафік).</param>
/// <param name="SamplingDuration">Вікно підрахунку збоїв; має бути щонайменше вдвічі більшим за AttemptTimeout.</param>
/// <param name="BreakDuration">Скільки ланцюг лишається розімкненим перед пробним запитом (Half-Open).</param>
/// <param name="GrpcMaxAttempts">Усього спроб у вбудованій retry-політиці gRPC-клієнта (не більше 5).</param>
public sealed record ResilienceProfile(
    string Service,
    int MaxRetryAttempts,
    TimeSpan RetryDelay,
    TimeSpan AttemptTimeout,
    TimeSpan TotalTimeout,
    double FailureRatio,
    int MinimumThroughput,
    TimeSpan SamplingDuration,
    TimeSpan BreakDuration,
    int GrpcMaxAttempts);

public static class ResilienceProfiles
{
    /// <summary>
    /// Критичний: без товарів картку замовлення не зібрати. Більше повторів (read-операції ідемпотентні),
    /// чутливіший circuit breaker (30 %) — швидко припинити навантажувати сервіс, що відмовляє, і дати йому відновитися.
    /// </summary>
    public static readonly ResilienceProfile Catalog = new(
        Service: "catalog-api",
        MaxRetryAttempts: 5,
        RetryDelay: TimeSpan.FromMilliseconds(200),
        AttemptTimeout: TimeSpan.FromSeconds(2),
        TotalTimeout: TimeSpan.FromSeconds(20),
        FailureRatio: 0.3,
        MinimumThroughput: 10,
        SamplingDuration: TimeSpan.FromSeconds(30),
        BreakDuration: TimeSpan.FromSeconds(10),
        GrpcMaxAttempts: 4);

    /// <summary>Критичний: замовлення — основа картки. Політика близька до каталогу, але таймаут спроби довший (запит складніший).</summary>
    public static readonly ResilienceProfile Orders = new(
        Service: "orders-api",
        MaxRetryAttempts: 3,
        RetryDelay: TimeSpan.FromMilliseconds(200),
        AttemptTimeout: TimeSpan.FromSeconds(3),
        TotalTimeout: TimeSpan.FromSeconds(15),
        FailureRatio: 0.3,
        MinimumThroughput: 10,
        SamplingDuration: TimeSpan.FromSeconds(30),
        BreakDuration: TimeSpan.FromSeconds(10),
        GrpcMaxAttempts: 3);

    /// <summary>
    /// Некритичний: без відгуків Aggregator повертає часткову відповідь. Швидко здатися: один повтор, короткі таймаути,
    /// менш чутливий circuit breaker (70 %), але довша пауза (30 с), щоб не витрачати час на сервіс, який не потрібен для відповіді.
    /// </summary>
    public static readonly ResilienceProfile Reviews = new(
        Service: "reviews-api",
        MaxRetryAttempts: 1,
        RetryDelay: TimeSpan.FromMilliseconds(200),
        AttemptTimeout: TimeSpan.FromSeconds(2),
        TotalTimeout: TimeSpan.FromSeconds(5),
        FailureRatio: 0.7,
        MinimumThroughput: 20,
        SamplingDuration: TimeSpan.FromSeconds(30),
        BreakDuration: TimeSpan.FromSeconds(30),
        GrpcMaxAttempts: 2);
}
