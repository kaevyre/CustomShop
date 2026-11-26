# aspirate-output

Kubernetes-маніфести платформи, згенеровані `aspirate generate` з моделі `Platform.AppHost/AppHost.cs` (Лаб. 10, частина 1).

## Склад

* **Проєкти .NET** (`orders-api`, `catalog-api`, `reviews-api`, `auth-api`, `identityserver`, `payments-api`, `aggregator`, `gateway`) — `deployment.yaml` + `service.yaml`; образи зібрані `dotnet publish /t:PublishContainer`, тег `latest`, `imagePullPolicy: IfNotPresent`.
* **Контейнери** (`sql`, `identity-sql`, `identityserver-sql`, `redis`, `messaging`, `mongo`, `keycloak`) — `statefulset.yaml` + `service.yaml`.
* `dashboard.yaml` — Aspire Dashboard (приймає OTLP-телеметрію сервісів), `kustomization.yaml` — корінь для `kubectl apply -k`.
* Відкриті налаштування — `configMapGenerator` у `kustomization.yaml` кожного ресурсу; чутливі значення — `secretGenerator` із файлів `.<ресурс>.secrets`.

## Секрети

Aspirate зберігає значення зашифрованими в `Platform.AppHost/aspirate-state.json` (поза git) і створює файли `.<ресурс>.secrets` лише на час `aspirate apply`; вони ігноруються через `.gitignore`. Частину значень (`Jwt__SecretKey`, паролі контейнерів) aspirate кладе в ConfigMap-літерали, тому перед комітом їх треба прибрати: `./scripts/redact-aspirate-secrets.ps1` (значення стають `<redacted>`). Для повторного розгортання виконайте `aspirate generate`.

## Виправлення після generate

Aspirate додає до Service і Deployment п'яти .NET-сервісів фіктивний порт `https`, що дублює порт gRPC/HTTP, і `kubectl apply` відхиляє такі маніфести. Він видаляється скриптом `./scripts/fix-aspirate-ports.ps1` (запускається після `aspirate generate`, до `aspirate apply`).

## Мережа

Порти сервісів беруться з `Kestrel:Endpoints` застосунків (orders-api 5080/5180, catalog-api 5081/5181, reviews-api 5082/5182 тощо), Gateway — `8080`:

```powershell
kubectl port-forward service/gateway 8080:8080
```
