# Ручне розгортання в Kubernetes (Лаб. 10, частини 2–3)

`catalog-api` і `gateway` розгортаються з власних Dockerfile та YAML-маніфестів. Для роботи `catalog-api` поруч піднімаються SQL Server, Redis і RabbitMQ (`sqlserver-*`, `redis-*`, `rabbitmq-*`).

| Файл | Призначення |
|---|---|
| `catalog-api-deployment.yaml` / `catalog-api-service.yaml` | 2 репліки, `imagePullPolicy: IfNotPresent`, порти 8080 (REST) і 8081 (gRPC), liveness `/health/live`, readiness `/health/ready`, startup `/health/startup`; ClusterIP `catalog-api` (80→8080) |
| `gateway-deployment.yaml` / `gateway-service.yaml` | те саме для YARP Gateway; ClusterIP `gateway` (80→8080) |
| `configmap.yaml` | `gateway-config` (адреси сервісів `services__<ім'я>__http__0`), `catalog-config`, `sqlserver-config` — відкриті дані |
| `sqlserver-*`, `redis-*`, `rabbitmq-*` | інфраструктура для `catalog-api` (демо, без постійних томів) |
| `create-secrets.ps1` / `create-secrets.sh` | створюють Secrets із змінних оточення — значень у репозиторії немає |
| `mint-dev-token.ps1` | видає dev-JWT для перевірки `/api/products` |

## Health checks із Лаб. 07

`/health/live`, `/health/ready` і `/health/startup` реєструє `MapDefaultEndpoints()` у `Platform.ServiceDefaults/Extensions.cs` **без умови на середовище** (на відміну від `/health` шаблону ServiceDefaults, що доступний лише в Development), тому працюють у `ASPNETCORE_ENVIRONMENT=Production`. Liveness не залежить від зовнішніх ресурсів, readiness перевіряє БД (`Unhealthy`) і Redis (`Degraded` — кеш необов'язковий, тож под залишається Ready).

## Порядок розгортання

```powershell
# 1. Образи (з кореня репозиторію; Docker Desktop передає їх кластеру без реєстру)
docker build -t catalog-api:v1 -f Services/Catalog/Catalog.Api/Dockerfile .
docker build -t gateway:v1     -f Platform.ApiGateway/Dockerfile .

# 2. Секрети (значення — лише у змінних оточення)
$env:SQL_PASSWORD      = '<пароль SA>'
$env:JWT_SECRET_KEY    = '<ключ підпису JWT з Лаб. 09>'
$env:RABBITMQ_PASSWORD = '<пароль RabbitMQ>'
./deploy/k8s/create-secrets.ps1

# 3. Маніфести
kubectl apply -f deploy/k8s/configmap.yaml
kubectl apply -f deploy/k8s/sqlserver-deployment.yaml -f deploy/k8s/sqlserver-service.yaml `
              -f deploy/k8s/redis-deployment.yaml -f deploy/k8s/redis-service.yaml `
              -f deploy/k8s/rabbitmq-deployment.yaml -f deploy/k8s/rabbitmq-service.yaml
kubectl apply -f deploy/k8s/catalog-api-deployment.yaml -f deploy/k8s/catalog-api-service.yaml
kubectl apply -f deploy/k8s/gateway-deployment.yaml -f deploy/k8s/gateway-service.yaml
kubectl get pods,services
```

Якщо `aspirate apply` уже розгорнуто платформу — спершу `aspirate destroy`, інакше імена Service збігаються.

> `catalog-api` застосовує міграції EF Core на старті, тому поки SQL Server не готовий, поди можуть кілька разів перезапуститись (CrashLoopBackOff) — це нормально, вони піднімуться самі. Дві репліки сідять БД одночасно; якщо побачите дублікати/помилку сідінгу — `kubectl scale deploy/catalog-api --replicas=1`, дочекайтесь Ready і поверніть `--replicas=2`.

## Перевірка service discovery

Gateway з Лаб. 09 вимагає JWT (маршрут `/api/products` має політику `default`, а `catalog-api` — permission `products:read`), тому запит без токена отримає 401 вже від Gateway. Dev-токен підписується тим самим ключем, що й Secret:

```powershell
kubectl port-forward service/gateway 8080:80          # окремий термінал
$t = ./deploy/k8s/mint-dev-token.ps1                   # потрібен $env:JWT_SECRET_KEY
curl.exe http://localhost:8080/api/products                              # 401 — токена немає
curl.exe -H "Authorization: Bearer $t" http://localhost:8080/api/products   # 200 — Gateway -> catalog-api
kubectl logs deploy/gateway | Select-String catalog
```

Звернення Gateway до `http://catalog-api` розв'язує вбудований DNS Kubernetes (ім'я Service у тому ж namespace) — IP-адреси ніде не прописані.

## Контрольні точки

```powershell
kubectl delete pod -l app=catalog-api --wait=false     # Deployment одразу створює новий под
kubectl get pods -l app=catalog-api -w

kubectl scale deploy/sql --replicas=0                  # база недоступна
kubectl get pods -l app=catalog-api                    # 0/1 Running: NotReady (readiness), RESTARTS не зростає (liveness не чіпає БД)
kubectl scale deploy/sql --replicas=1                  # под повертається в Ready

kubectl patch configmap gateway-config --type merge -p '{"data":{"ASPNETCORE_ENVIRONMENT":"Development"}}'
kubectl rollout restart deploy/gateway                 # ConfigMap підхоплюється лише після рестарту подів

kubectl get secret gateway-secrets -o yaml             # значення лише в Base64
kubectl get secret gateway-secrets -o jsonpath='{.data.Jwt__SecretKey}' | % { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($_)) }
```

> Secrets за замовчуванням лише закодовані в Base64, а не зашифровані. У production — шифрування etcd і зовнішні сховища (HashiCorp Vault, Azure Key Vault).
> Після перевірки поверніть `ASPNETCORE_ENVIRONMENT: "Production"` у `configmap.yaml` і виконайте `kubectl apply -f deploy/k8s/configmap.yaml`.

## Порівняння з маніфестами Aspirate (`Platform.AppHost/aspirate-output/`)

| Аспект | Aspirate | Ручні маніфести |
|---|---|---|
| Охоплення | усі ресурси AppHost (8 проєктів, 7 контейнерів, Aspire Dashboard) | `catalog-api`, `gateway` + мінімальна інфраструктура |
| Образи | `dotnet publish /t:PublishContainer`, тег `latest` | багатоетапний Dockerfile, тег `v1` |
| Контейнери БД/брокерів | `StatefulSet` | `Deployment` (демо, `emptyDir`) |
| Репліки | 1, `strategy: Recreate`, `minReadySeconds: 60` | 2, `RollingUpdate` |
| Probes | немає — Aspire сам перевіряє health через AppHost | liveness `/health/live`, readiness `/health/ready`, startup `/health/startup` |
| Порти | з `Kestrel:Endpoints` (5081, 5181 …), Gateway — 8080, 8443, 5000 | 8080 (HTTP) і 8081 (gRPC) із Dockerfile; Service 80 → 8080 |
| Конфігурація | `configMapGenerator` з літералів у `kustomization.yaml` кожного ресурсу | окремий `configmap.yaml` |
| Секрети | `secretGenerator` із `.secrets`-файлів, значення зашифровані в `aspirate-state.json`; `Jwt__SecretKey` і паролі контейнерів потрапляють у ConfigMap відкритим текстом | `kubectl create secret`, значення зі змінних оточення, у YAML їх немає |
| Ресурси (requests/limits) | не задаються | задані |
