using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>
/// Топологія брокера. Оголошується декларативно в коді під час старту (кожним сервісом, якому вона потрібна):
/// повторне оголошення тієї самої черги з тими самими параметрами безпечне. Параметри створеної черги
/// вручну в Management UI не змінюються — інакше наступний старт завершиться PRECONDITION_FAILED.
///
///   catalog-events (topic) ──product.created / product.price_changed / product.renamed / product.deleted──►
///        orders.catalog-sync   (Замовлення: копія довідника Products)
///        reviews.catalog-sync  (Відгуки: денормалізований productName)
///   Відхилені повідомлення: черга ──x-dead-letter-exchange──► catalog-events.dlx (direct) ──► {черга}.dlq
/// </summary>
public static class Topology
{
    public const string CatalogExchange = "catalog-events";
    public const string CatalogDeadLetterExchange = "catalog-events.dlx";

    public const string OrdersCatalogSyncQueue = "orders.catalog-sync";
    public const string ReviewsCatalogSyncQueue = "reviews.catalog-sync";

    public static string DeadLetterQueueOf(string queue) => $"{queue}.dlq";

    public static class RoutingKeys
    {
        public const string ProductCreated = "product.created";
        public const string ProductPriceChanged = "product.price_changed";
        public const string ProductRenamed = "product.renamed";
        public const string ProductDeleted = "product.deleted";
    }

    /// <summary>Усі exchanges, черги, bindings і DLQ каталогу подій.</summary>
    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(CatalogExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        // direct, а не fanout: кожна DLQ прив'язана за routing key = ім'я черги-джерела, тож «мертві» повідомлення
        // orders.catalog-sync не потрапляють у reviews.catalog-sync.dlq
        await channel.ExchangeDeclareAsync(CatalogDeadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: ct);

        await DeclareConsumerQueueAsync(channel, OrdersCatalogSyncQueue,
            [RoutingKeys.ProductCreated, RoutingKeys.ProductPriceChanged, RoutingKeys.ProductRenamed, RoutingKeys.ProductDeleted], ct);

        // Відгуки цікавить лише перейменування (productName)
        await DeclareConsumerQueueAsync(channel, ReviewsCatalogSyncQueue, [RoutingKeys.ProductRenamed], ct);
    }

    private static async Task DeclareConsumerQueueAsync(
        IChannel channel, string queue, IEnumerable<string> routingKeys, CancellationToken ct)
    {
        var dlq = DeadLetterQueueOf(queue);

        // DLQ: durable, без споживача — повідомлення чекають на ручний розбір
        await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(dlq, CatalogDeadLetterExchange, routingKey: queue, cancellationToken: ct);

        // основна черга: durable + dead-letter на DLX
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = CatalogDeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue
            },
            cancellationToken: ct);

        foreach (var key in routingKeys)
            await channel.QueueBindAsync(queue, CatalogExchange, routingKey: key, cancellationToken: ct);
    }
}
