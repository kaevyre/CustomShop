# Скрипти баз даних

Три самостійні бази, кожна для свого контексту. Між ними немає спільних таблиць і жодного FOREIGN KEY. Таблицю політики дублювання див. у [`../README.md`](../README.md#4-політика-дублювання-даних) та [`../docs/duplication-policy.md`](../docs/duplication-policy.md).

| Папка | Контекст | СУБД | БД |
|---|---|---|---|
| `p1/` | Замовлення | SQL Server (ADO.NET & Dapper) | `CustomShop_Orders` |
| `p2/` | Каталог | SQL Server (EF Core) | `CustomShop_Catalog` |
| `p3/` | Відгуки та галерея | MongoDB | `customshop_reviews` |

## Розгортання однією командою

| Файл | Призначення |
|---|---|
| `docker-compose.yml` | SQL Server 2022 (`customshop-sql`, порт 1433) і MongoDB 7 (`customshop-mongo`, порт 27017); папка `db/` змонтована в контейнери як `/db` |
| `deploy.ps1` | Windows: піднімає контейнери (з `-Docker`) і виконує скрипти по порядку, зупиняється на першій помилці |
| `deploy.sh` | те саме для Linux / macOS / Git Bash |

```powershell
.\db\deploy.ps1 -Docker -Checks
```
```bash
./db/deploy.sh --docker --checks
```

Без Docker скрипти використовують локальні `sqlcmd` і `mongosh` (параметри див. у кореневому README, розділ 8).

## Порядок запуску вручну (з кореня репозиторію)

```bash
# Проєкт №1
sqlcmd -S localhost -U sa -P "<пароль>" -C -I -f 65001 -i db/p1/schema.sql
sqlcmd -S localhost -U sa -P "<пароль>" -C -I -f 65001 -i db/p1/procedures.sql
sqlcmd -S localhost -U sa -P "<пароль>" -C -I -f 65001 -i db/p1/seed.sql
sqlcmd -S localhost -U sa -P "<пароль>" -C -I -f 65001 -i db/p1/checks.sql     # перевірка

# Проєкт №2
sqlcmd -S localhost -U sa -P "<пароль>" -C -I -f 65001 -i db/p2/schema.sql
sqlcmd -S localhost -U sa -P "<пароль>" -C -I -f 65001 -i db/p2/seed.sql
sqlcmd -S localhost -U sa -P "<пароль>" -C -I -f 65001 -i db/p2/checks.sql     # перевірка

# Проєкт №3
mongosh "mongodb://localhost:27017" db/p3/collections.js
mongosh "mongodb://localhost:27017" db/p3/seed.js
mongosh "mongodb://localhost:27017" db/p3/queries.js                          # перевірка
```

У SSMS: відкрити файл і натиснути **Execute (F5)** у тому самому порядку.

## Ідемпотентність

- `seed.sql` (p1, p2): кожна вставка має `WHERE NOT EXISTS` за ключем або Email, тому повторний запуск не створює дублікатів.
- `seed.js` (p3): кожен документ має фіксований `_id` і вставляється через `updateOne(..., { $setOnInsert }, { upsert: true })`.
- `collections.js`: наявна колекція не перестворюється, її валідатор оновлюється через `collMod`; `createIndex` не дублює індекси.
- `schema.sql` перестворює таблиці з нуля, тобто видаляє наявні дані.
