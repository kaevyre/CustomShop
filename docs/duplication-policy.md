# Політика дублювання даних і узгодженості між базами


| Поле | Де зберігається | Джерело (власник) | Політика узгодженості |
|---|---|---|---|
| `OrderItems.ProductName`, `UnitPrice` | БД Замовлень | Product (БД Каталогу) | знімок на момент оформлення — не оновлюється |
| `OrderItemOptions.OptionId`, `OptionGroup`, `OptionValue`, `PriceDelta` | БД Замовлень | CustomizationOption (БД Каталогу) | знімок на момент оформлення — не оновлюється |
| `Products.Sku`, `Name`, `Price`, `Stock`, `IsActive` (локальна копія) | БД Замовлень | Product (БД Каталогу) | оновлюється подіями з Каталогу (`ProductCreated`, `ProductUpdated`, `StockChanged`); `SyncedAt` — час останньої події |
| `reviews.productName`, `productType` | MongoDB | Product (БД Каталогу) | оновлюється подіями з Каталогу (`onProductRenamed` у `queries.js`) |
| `discussions.productId`, `reviews.productId`, `builds.items[].productId` | MongoDB | Product (БД Каталогу) | лише ідентифікатор, без FK |
| `builds.items[].productName`, `productType` | MongoDB | Product (БД Каталогу) | знімок на момент публікації сетапу — не оновлюється |
| `reviews.verifiedPurchase`, `reviews.orderId` | MongoDB | Order (БД Замовлень) | встановлюється подією `OrderShipped` (`onOrderShipped`) |
| `userProfiles.customerId` | MongoDB | Customer (БД Замовлень) | лише ідентифікатор, без FK; профіль створюється подією `CustomerRegistered` |
| `reviews.author.displayName`, `builds.author.displayName`, `discussions.messages[].author.displayName` | MongoDB | userProfiles (та сама БД) | денормалізація для читання; оновлюється `updateMany` при зміні профілю (`onProfileRenamed`) |

Пари контекстів, що взаємодіють: **Каталог ↔ Замовлення**, **Каталог ↔ Відгуки**, **Замовлення ↔ Відгуки** — для кожної є щонайменше один рядок.

## Чому немає FOREIGN KEY між базами

Кожна база — окремий мікросервіс зі своїм сервером/схемою. Посилання на чужу сутність зберігається як звичайний стовпець або поле з її Id:

| Посилання | Де | На що вказує | Як підтримується |
|---|---|---|---|
| `Products.Id` | БД Замовлень (`db/p1`) | `Products.Id` у Каталозі | події `ProductCreated` / `ProductUpdated` / `StockChanged` |
| `OrderItemOptions.OptionId` | БД Замовлень | `CustomizationOptions.Id` у Каталозі | знімок, без FK |
| `reviews.productId`, `builds.items[].productId`, `discussions.productId` | MongoDB (`db/p3`) | `Products.Id` у Каталозі | без FK, назва — денормалізована копія |
| `reviews.orderId` | MongoDB | `Orders.Id` у БД Замовлень | подія `OrderShipped` |
| `userProfiles.customerId` | MongoDB | `Customers.Id` у БД Замовлень | подія `CustomerRegistered` |

Перевірка: `SELECT * FROM sys.foreign_keys` у кожній SQL-базі показує посилання лише на таблиці цієї ж бази (`db/p1/checks.sql`, `db/p2/checks.sql`, блок 1).
