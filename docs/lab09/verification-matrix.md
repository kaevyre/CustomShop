# Таблиця перевірки матриці permissions (Keycloak, Частина 3)

Запити йдуть через Gateway (`http://localhost:5000`) на **неіснуючий** id `999999999`: якщо доступ дозволено — сервіс відповідає 404/400 (без побічних ефектів), якщо заборонено — **403**.
Тому «дозволено» тут = будь-який код, крім 401/403. Колонку «факт» заповніть після прогону колекції Postman (папки ролей).

| Сервіс | Endpoint | Permission | admin (очік. / факт) | manager (очік. / факт) | user (очік. / факт) | guest (очік. / факт) |
|---|---|---|---|---|---|---|
| Orders | `GET /api/orders/{id}` | `orders:read` | ≠403 / ___ | ≠403 / ___ | ≠403 / ___ | 403 / ___ |
| Orders | `POST /api/orders` | `orders:create` | ≠403 / ___ | 403 / ___ | ≠403 / ___ | 403 / ___ |
| Orders | `PUT /api/orders/{id}` | `orders:update` | ≠403 / ___ | ≠403 / ___ | 403 / ___ | 403 / ___ |
| Orders | `DELETE /api/orders/{id}` | `orders:delete` | ≠403 / ___ | 403 / ___ | 403 / ___ | 403 / ___ |
| Orders | `POST /api/orders/{id}/approve` | `orders:approve` | ≠403 / ___ | ≠403 / ___ | 403 / ___ | 403 / ___ |
| Products | `GET /api/products` | `products:read` | ≠403 / ___ | ≠403 / ___ | ≠403 / ___ | ≠403 / ___ |
| Products | `POST /api/products` | `products:create` | ≠403 / ___ | 403 / ___ | 403 / ___ | 403 / ___ |
| Products | `PUT /api/products/{id}/price` | `products:update` | ≠403 / ___ | ≠403 / ___ | 403 / ___ | 403 / ___ |
| Products | `DELETE /api/products/{id}` | `products:delete` | ≠403 / ___ | 403 / ___ | 403 / ___ | 403 / ___ |
| Products | `POST /api/products/{id}/publish` | `products:publish` | ≠403 / ___ | ≠403 / ___ | 403 / ___ | 403 / ___ |
| Payments | `GET /api/payments` | `payments:read` | ≠403 / ___ | 403 / ___ | 403 / ___ | 403 / ___ |
| Payments | `POST /api/payments/process` | `payments:process` | ≠403 / ___ | 403 / ___ | 403 / ___ | 403 / ___ |
| Payments | `POST /api/payments/refund` | `payments:refund` | ≠403 / ___ | 403 / ___ | 403 / ___ | 403 / ___ |

Без токена будь-який endpoint → **401**.
