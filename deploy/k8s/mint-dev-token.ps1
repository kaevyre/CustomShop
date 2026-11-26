# Видає dev-JWT для перевірки /api/products через Gateway (Лаб. 09 вимагає токен із permission products:read).
# Токен підписується тим самим ключем, що й у Secret (JWT_SECRET_KEY), issuer/audience — як у catalog-config.
#   $env:JWT_SECRET_KEY='<ключ>'; $t = .\mint-dev-token.ps1; curl.exe -H "Authorization: Bearer $t" http://localhost:8080/api/products
if (-not $env:JWT_SECRET_KEY) { throw 'Задайте JWT_SECRET_KEY' }
function B64Url([byte[]]$b) { [Convert]::ToBase64String($b).TrimEnd('=').Replace('+', '-').Replace('/', '_') }
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$header  = B64Url ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
$payload = B64Url ([Text.Encoding]::UTF8.GetBytes((@{
    iss = 'auth-service'; aud = 'microservices-platform'; sub = 'k8s-demo'
    scope = 'products.read'; iat = $now; exp = $now + 3600
} | ConvertTo-Json -Compress)))
$hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($env:JWT_SECRET_KEY))
$sig = B64Url ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes("$header.$payload")))
"$header.$payload.$sig"
