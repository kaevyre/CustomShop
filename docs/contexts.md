# Предметна область і контексти (Bounded Contexts)

## 1. Предметна область

Інтернет-магазин кастомних комп'ютерних аксесуарів: клавіатур, мишей, навушників, колонок і килимків. Покупець обирає базовий товар і налаштовує його (тип світчів, кейкапи, колір корпусу, амбушури, принт килимка), оформлює замовлення, яке магазин виготовляє та відвантажує. Після покупки клієнти залишають відгуки та публікують фото своїх сетапів у галереї.

## 2. Контексти (Bounded Contexts)

| Контекст | Проєкт / технологія | БД | Сутності, якими володіє лише цей контекст | Чужі дані (лише Id або копія, не власник) |
|---|---|---|---|---|
| **Замовлення** | №1 — SQL Server + ADO.NET & Dapper | `CustomShop_Orders` | Customer, Order, OrderDetails, OrderItem, OrderItemOption | таблиця `Products` — локальна копія (read model) товарів Каталогу; `OrderItemOptions.OptionId` → опція Каталогу |
| **Каталог** | №2 — SQL Server + EF Core | `CustomShop_Catalog` | Brand, Category, Product, ProductCategory, ProductSpecification, CustomizationOption | — |
| **Відгуки та галерея** | №3 — MongoDB | `customshop_reviews` | UserProfile (`userProfiles`), Review (`reviews`), Discussion (`discussions`), Build (`builds`) | `productId`, `productName` → Каталог; `orderId`, `customerId` → Замовлення |

**Product** належить лише Каталогу: таблиця `Products` у БД Замовлень — це не окрема сутність, а локальна копія потрібних для оформлення полів (Sku, Name, Price, Stock, IsActive), яку оновлюють події з Каталогу (див. політику дублювання). Замовлення ці дані не створюють і не редагують як власник — лише резервують склад у межах свого сервісу.

Кожна сутність має одного власника. Інші контексти зберігають лише її ідентифікатор і, за потреби, знімок окремих полів. Між базами немає спільних таблиць і жодного FOREIGN KEY: посилання на сутність іншого контексту — звичайний стовпець/поле з її Id (`Products.Id`, `OrderItemOptions.OptionId` у БД Замовлень; `productId`, `orderId`, `customerId` у MongoDB). Усі FOREIGN KEY в `db/p1/schema.sql` і `db/p2/schema.sql` посилаються лише на таблиці своєї бази.

## 3. Агрегати

| Контекст | Агрегат (корінь + пов'язані об'єкти) | Життєвий цикл |
|---|---|---|
| Замовлення | **Order** + OrderDetails + OrderItems + OrderItemOptions | позиції та опції не існують без замовлення, змінюються лише через нього (процедури); видалення замовлення каскадно видаляє їх |
| Замовлення | **Customer** | м'яке видалення; фізично не видаляється, поки є історія замовлень |
| Каталог | **Product** + ProductSpecification + CustomizationOptions (+ зв'язки ProductCategories) | специфікація та опції каскадно видаляються разом із товаром |
| Каталог | **Brand**, **Category** | довідники |
| Відгуки | **Review** + вбудовані author, comments, customization | коментарі живуть у відгуку |
| Відгуки | **Discussion** (+ вбудовані messages) | посилається на відгук, може бути дуже великою |
| Відгуки | **Build** (+ вбудований масив items) | знімок сетапу на момент публікації |
| Відгуки | **UserProfile** | публічний профіль, пов'язаний з Customer через customerId |

### Статуси замовлення

```
Submitted → AwaitingValidation → StockConfirmed → Paid → InProduction → Shipped
    └──────────────┴────────────────────┴──→ Cancelled   (лише до оплати)
```

`InProduction` — етап виготовлення кастомного товару (відмінність від eShop).

## 4. Ключі, конкурентність, аудит

**Первинні ключі**

- `BIGINT IDENTITY` для Customers, Orders, OrderItems (Проєкт №1) і Products, CustomizationOptions (Проєкт №2). Записи створює один сервіс, тож центрального генератора не потрібно; послідовні ключі дають компактний кластерний індекс без фрагментації.
- `INT IDENTITY` для малих довідників Brands і Categories.
- `Products.Id` у БД Замовлень **не** IDENTITY: це той самий Id, що в Каталозі.
- `OrderItems` має сурогатний PK, а не `(OrderId, ProductId)`: в одному замовленні може бути той самий товар двічі з різними налаштуваннями.
- MongoDB: `ObjectId` генерується клієнтом без координації між вузлами і впорядкований за часом.

**Каскади.** `ON DELETE CASCADE` — лише для частин агрегату (OrderDetails, OrderItems, OrderItemOptions, ProductSpecifications, ProductCategories, CustomizationOptions). Customers → Orders і Brands → Products — без каскаду: клієнта з історією замовлень чи бренд із товарами видалити не можна.

**Аудит і м'яке видалення.** `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted` у Customers, Orders, Brands, Categories, Products. Фільтровані індекси (`WHERE IsDeleted = 0`) працюють лише з «живими» записами.

**Оптимістична конкурентність — `ROWVERSION`:**

- `Orders` — статус одночасно можуть змінювати менеджер і платіжний сервіс;
- `Customers` — профіль редагують клієнт і підтримка;
- `Products` (обидві SQL-бази) — ціну та склад змінюють менеджер, резервування замовлень і обробник подій.

Процедури приймають `@ExpectedRowVersion`: якщо запис уже змінив інший користувач, повертається помилка 50003 замість «тихого» перезапису. В EF Core (Лаб. 03) це властивість з `.IsRowVersion()`.
