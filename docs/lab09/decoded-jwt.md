# Декодовані JWT (заповнюється після запуску; jwt.io)

Нижче — **очікувана** форма кожного токена, за якою перевіряється результат. Замініть блоки реальними декодованими payload.

## 1. Auth Service (Identity) — користувач з роллю User

```json
{
  "sub": "<userId>",
  "unique_name": "testuser",
  "email": "testuser@platform.local",
  "jti": "<guid>",
  "role": "User",
  "exp": "<iat + 15 хв>",
  "iss": "auth-service",
  "aud": "microservices-platform"
}
```

## 2. IdentityServer — Client Credentials (aggregator_service)

Немає claim `sub` користувача — лише ідентичність сервісу (`client_id`); `aud` = `orders_api`, `products_api`.

```json
{
  "iss": "http://localhost:5000",
  "client_id": "aggregator_service",
  "aud": ["orders_api", "products_api"],
  "scope": ["orders.read", "products.read", "orders_api", "products_api"]
}
```

## 3. Keycloak — john.manager (роль manager)

```json
{
  "iss": "http://localhost:8080/realms/DevRealmDrivers",
  "aud": ["orders-api", "products-api", "payments-api"],
  "sub": "<keycloak user id>",
  "preferred_username": "john.manager",
  "realm_access": { "roles": ["manager"] },
  "authorization": {
    "permissions": [
      { "rsname": "orders-api",   "scopes": ["orders:read", "orders:update", "orders:approve"] },
      { "rsname": "products-api", "scopes": ["products:read", "products:update", "products:publish"] }
    ]
  }
}
```

Очікувана поведінка: `PUT /api/orders/{id}` → не 403, `POST /orders/{id}/approve` → 2xx, `DELETE /orders/{id}` → **403**, будь-який `payments-api` → **403**.
