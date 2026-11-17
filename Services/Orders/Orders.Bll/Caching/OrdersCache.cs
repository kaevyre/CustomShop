namespace Orders.Bll.Caching;

/// <summary>
/// Ключі кешу сервісу Замовлень: префікс orders: ізолює їх від інших сервісів у спільному Redis.
/// Кешуються лише знеособлені дані (довідник товарів, статистика); замовлення з персональними даними покупця — ні.
/// </summary>
public static class OrdersCacheKeys
{
    public const string Statistics = "orders:statistics";

    /// <summary>Ключ-версія для всіх сторінок довідника товарів.</summary>
    public const string ProductListsVersion = "orders:products:list:version";

    public static string Product(long id) => $"orders:product:{id}";

    public static string ProductsList(string version, int page, int pageSize) =>
        $"orders:products:list:v{version}:p{page}:s{pageSize}";

    /// <summary>Агрегована картка замовлення в Aggregator: зміна статусу прибирає її зі спільного Redis.</summary>
    public static string AggregatedOrder(long orderId) => $"aggregator:order:{orderId}:full";
}

public static class OrdersCacheProfiles
{
    public static readonly CacheProfile Product = new(
        L1Sliding: TimeSpan.FromSeconds(30),
        L1Absolute: TimeSpan.FromMinutes(1),
        L2Ttl: TimeSpan.FromMinutes(10));

    /// <summary>Статистика змінюється з кожним замовленням, тому живе недовго.</summary>
    public static readonly CacheProfile Statistics = new(
        L1Sliding: TimeSpan.FromSeconds(5),
        L1Absolute: TimeSpan.FromSeconds(10),
        L2Ttl: TimeSpan.FromSeconds(60));

    public static CacheProfile List(int pageSize) => new(
        L1Sliding: TimeSpan.FromSeconds(15),
        L1Absolute: TimeSpan.FromSeconds(30),
        L2Ttl: TimeSpan.FromMinutes(5),
        Size: pageSize);
}
