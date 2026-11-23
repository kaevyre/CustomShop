namespace Platform.EventBus.Events;

// Події Каталогу (Лаб. 03) — джерело правди про товар. Назви в минулому часі: це факти, що вже сталися.
// Кожна подія самодостатня: споживач не звертається до Каталогу по додаткові дані. Подія містить
// знімок полів, потрібних споживачам (Sku, ім'я, ціна, ознака активності), тож будь-яка подія застосовується
// як «остання відома версія товару» за часом OccurredAt — це робить обробку стійкою до порушення порядку.
// Policy з Лаб. 01: ProductCreated / ProductUpdated(→ PriceChanged, Renamed) / StockChanged.

/// <summary>Товар створено. <see cref="Stock"/> — початковий залишок (споживач використовує його лише при вставці копії).</summary>
public sealed record ProductCreatedEvent(
    long ProductId, string Sku, string ProductName, decimal Price, int Stock, bool IsActive) : IntegrationEvent;

/// <summary>Змінено ціну товару. Ціни в уже оформлених замовленнях (OrderItems.UnitPrice) не змінюються — це знімок на момент покупки.</summary>
public sealed record ProductPriceChangedEvent(
    long ProductId, string Sku, string ProductName, decimal OldPrice, decimal NewPrice, bool IsActive) : IntegrationEvent;

/// <summary>Товар перейменовано: Відгуки оновлюють денормалізований productName, Замовлення — Products.Name.</summary>
public sealed record ProductRenamedEvent(
    long ProductId, string Sku, string OldName, string NewName, decimal Price, bool IsActive) : IntegrationEvent;

/// <summary>Товар видалено (м'яке видалення в Каталозі): споживачі позначають копію неактивною, Id лишається.</summary>
public sealed record ProductDeletedEvent(
    long ProductId, string Sku, string ProductName, decimal Price) : IntegrationEvent;
