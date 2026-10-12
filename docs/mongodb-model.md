# Проєкт №3 — модель документів MongoDB

БД `customshop_reviews`, 4 колекції. Скрипт створення — `db/p3/collections.js`, дані — `db/p3/seed.js`, приклади документів — [`sample-documents.json`](sample-documents.json).


| Колекція | Embedding | Referencing | Гнучка схема |
|---|---|---|---|
| `userProfiles` | `socials`, `stats` | `customerId` → БД Замовлень | `bio`, `socials`, `avatarUrl` є не в усіх |
| `reviews` | `author`, `comments[]`, `customization` | `productId` → Каталог, `orderId` → Замовлення, `discussionId` → discussions, `author.profileId` → userProfiles | `photos`, `pros`, `cons`, `title`, ключі `customization` різні |
| `discussions` | `messages[]` з вбудованим автором | `reviewId` → reviews | повідомлення з профілем і без |
| `builds` | `items[]` з `customization`, `author` | `items[].productId` → Каталог, `author.profileId` → userProfiles | у кожного типу товару свої поля налаштувань |

**Гібридний підхід:** автор у `reviews` / `builds` = посилання (`profileId`) + знімок імені (`displayName`). Короткі коментарі вбудовані у відгук (обмеження `maxItems: 50`), а довге обговорення винесене в `discussions` за посиланням.

## Embedding чи referencing — чому саме так

| Дані | Рішення | Причина |
|---|---|---|
| автор відгуку | **гібрид**: `profileId` + `displayName` | ім'я потрібне при кожному показі відгуку, повний профіль — рідко |
| коментарі до відгуку | **embedding** (`comments[]`, до 50) | кілька коротких реплік, завжди показуються разом з відгуком |
| довге обговорення | **referencing** (`discussionId` → `discussions`) | може містити тисячі повідомлень, документ відгуку не повинен рости без меж |
| налаштування товару | **embedding** (`customization`) | належать лише цьому відгуку/сетапу, у кожного типу товару свої поля |
| товари сетапу | **embedding** масиву `items[]` + `productId` | агрегат «сетап» читається цілком; назва — знімок на момент публікації |
| товар у Каталозі | **referencing** `productId` (без FK) | інший контекст, інша БД |

## Гнучка схема

В одній колекції лежать документи з різними наборами полів:

- `reviews`: повний відгук (фото, плюси/мінуси, налаштування, коментарі, обговорення) і мінімальний (лише обов'язкові поля);
- `builds.items[].customization`: у клавіатури `switches`, `keycaps`; у килимка `size`, `printUrl`; у навушників `earpads`, `engraving`; у колонок цього поля немає зовсім;
- `userProfiles`: з `socials`/`bio`/`stats` і без них.

JSON Schema Validation перевіряє лише обов'язкові поля та типи, тому додаткові поля дозволені.

## Індекси

| Індекс | Колекція | Запит |
|---|---|---|
| `ux_customerId` | userProfiles | знайти профіль за Id клієнта (обробка подій) |
| `ix_product_createdAt` | reviews | відгуки товару, нові першими |
| `ix_author_createdAt` | reviews | усі відгуки автора |
| `ux_product_author` | reviews | один автор — один відгук на товар |
| `tx_title_text` | reviews | повнотекстовий пошук |
| `ux_reviewId` | discussions | обговорення відгуку |
| `ix_lastMessageAt` | discussions | активні обговорення |
| `ix_items_productId` | builds | сетапи з товаром X |
| `ix_tags_likes` | builds | сетапи за тегом, популярні першими |
