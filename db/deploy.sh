#!/usr/bin/env bash
# =====================================================================
# Розгортання трьох баз CustomShop з нуля (Linux / macOS / Git Bash).
#
# Використання (з будь-якої папки):
#   ./db/deploy.sh                 усі три бази, локальні sqlcmd і mongosh
#   ./db/deploy.sh --docker        усі три бази в контейнерах з db/docker-compose.yml
#   ./db/deploy.sh p1 p3           лише вибрані проєкти (p1, p2, p3)
#   ./db/deploy.sh --checks        після розгортання виконати checks.sql / queries.js
#   ./db/deploy.sh --seed-only     лише повторити seed (перевірка ідемпотентності)
#
# Параметри підключення (змінні середовища):
#   SQL_SERVER   (localhost)     SQL_USER (sa)     SQL_PASSWORD (Str0ng!Passw0rd)
#   SQL_TRUSTED=1                Windows-автентифікація замість логіна/пароля
#   MONGO_URI    (mongodb://localhost:27017)
#
# Порядок:  p1: schema -> procedures -> seed
#           p2: schema -> seed
#           p3: collections -> seed
# Увага: schema.sql перестворює таблиці (наявні дані видаляються).
# =====================================================================
set -euo pipefail

DB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

SQL_SERVER="${SQL_SERVER:-localhost}"
SQL_USER="${SQL_USER:-sa}"
SQL_PASSWORD="${SQL_PASSWORD:-Str0ng!Passw0rd}"
SQL_TRUSTED="${SQL_TRUSTED:-0}"
MONGO_URI="${MONGO_URI:-mongodb://localhost:27017}"

USE_DOCKER=0
RUN_CHECKS=0
SEED_ONLY=0
PROJECTS=()

for arg in "$@"; do
  case "$arg" in
    --docker)    USE_DOCKER=1 ;;
    --checks)    RUN_CHECKS=1 ;;
    --seed-only) SEED_ONLY=1 ;;
    p1|p2|p3)    PROJECTS+=("$arg") ;;
    -h|--help)   sed -n '2,22p' "$0"; exit 0 ;;
    *) echo "Невідомий параметр: $arg (див. --help)" >&2; exit 1 ;;
  esac
done
[ ${#PROJECTS[@]} -eq 0 ] && PROJECTS=(p1 p2 p3)

step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

# ---------- виконання SQL-файлу ----------
run_sql() {
  local file="$1"
  step "SQL  $file"
  if [ "$USE_DOCKER" -eq 1 ]; then
    docker exec customshop-sql /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "$SQL_PASSWORD" -C -I -f 65001 -b -i "/db/$file"
  else
    local auth=(-U "$SQL_USER" -P "$SQL_PASSWORD")
    [ "$SQL_TRUSTED" = "1" ] && auth=(-E)
    sqlcmd -S "$SQL_SERVER" "${auth[@]}" -C -I -f 65001 -b -i "$DB_DIR/$file"
  fi
}

# ---------- виконання mongosh-скрипта ----------
run_mongo() {
  local file="$1"
  step "MONGO $file"
  if [ "$USE_DOCKER" -eq 1 ]; then
    docker exec customshop-mongo mongosh --quiet "/db/$file"
  else
    mongosh --quiet "$MONGO_URI" "$DB_DIR/$file"
  fi
}

# ---------- підготовка ----------
if [ "$USE_DOCKER" -eq 1 ]; then
  step "Запуск контейнерів (db/docker-compose.yml)"
  SQL_PASSWORD="$SQL_PASSWORD" docker compose -f "$DB_DIR/docker-compose.yml" up -d
  printf 'Очікування готовності SQL Server і MongoDB'
  for _ in $(seq 1 60); do
    sql_ok=$(docker inspect -f '{{.State.Health.Status}}' customshop-sql 2>/dev/null || true)
    mongo_ok=$(docker inspect -f '{{.State.Health.Status}}' customshop-mongo 2>/dev/null || true)
    [ "$sql_ok" = "healthy" ] && [ "$mongo_ok" = "healthy" ] && break
    printf '.'; sleep 3
  done
  echo
  [ "$sql_ok" = "healthy" ] && [ "$mongo_ok" = "healthy" ] || { echo "Контейнери не стали healthy" >&2; exit 1; }
else
  for p in "${PROJECTS[@]}"; do
    if [[ "$p" == p1 || "$p" == p2 ]]; then
      command -v sqlcmd >/dev/null || { echo "sqlcmd не знайдено. Встановіть його або запустіть з --docker" >&2; exit 1; }
    fi
    if [[ "$p" == p3 ]]; then
      command -v mongosh >/dev/null || { echo "mongosh не знайдено. Встановіть його або запустіть з --docker" >&2; exit 1; }
    fi
  done
fi

# ---------- розгортання ----------
for p in "${PROJECTS[@]}"; do
  case "$p" in
    p1)
      if [ "$SEED_ONLY" -eq 0 ]; then
        run_sql p1/schema.sql
        run_sql p1/procedures.sql
      fi
      run_sql p1/seed.sql
      [ "$RUN_CHECKS" -eq 1 ] && run_sql p1/checks.sql
      ;;
    p2)
      [ "$SEED_ONLY" -eq 0 ] && run_sql p2/schema.sql
      run_sql p2/seed.sql
      [ "$RUN_CHECKS" -eq 1 ] && run_sql p2/checks.sql
      ;;
    p3)
      [ "$SEED_ONLY" -eq 0 ] && run_mongo p3/collections.js
      run_mongo p3/seed.js
      [ "$RUN_CHECKS" -eq 1 ] && run_mongo p3/queries.js
      ;;
  esac
done

step "Готово: ${PROJECTS[*]}"
