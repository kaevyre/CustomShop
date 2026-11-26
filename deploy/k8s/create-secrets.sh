#!/usr/bin/env bash
# Те саме, що create-secrets.ps1, для bash. Значення — зі змінних оточення (у репозиторії їх немає).
set -euo pipefail
: "${SQL_PASSWORD:?Задайте SQL_PASSWORD}"
: "${JWT_SECRET_KEY:?Задайте JWT_SECRET_KEY}"
: "${RABBITMQ_PASSWORD:?Задайте RABBITMQ_PASSWORD}"
CATALOG_DB="Server=sql,1433;Database=CustomShop_Catalog;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True;Encrypt=False"

apply() { kubectl create secret generic "$@" --dry-run=client -o yaml | kubectl apply -f -; }

apply sql-secrets --from-literal=MSSQL_SA_PASSWORD="$SQL_PASSWORD"
apply rabbitmq-secrets --from-literal=RABBITMQ_DEFAULT_USER=platform --from-literal=RABBITMQ_DEFAULT_PASS="$RABBITMQ_PASSWORD"
apply catalog-secrets \
  --from-literal=ConnectionStrings__CatalogDb="$CATALOG_DB" \
  --from-literal=ConnectionStrings__messaging="amqp://platform:${RABBITMQ_PASSWORD}@rabbitmq:5672" \
  --from-literal=ConnectionStrings__redis=redis:6379 \
  --from-literal=Jwt__SecretKey="$JWT_SECRET_KEY"
apply gateway-secrets --from-literal=Jwt__SecretKey="$JWT_SECRET_KEY"
