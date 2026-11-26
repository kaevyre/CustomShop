# Створює Secrets у кластері. Значення беруться зі змінних оточення — у репозиторії їх немає.
#   $env:SQL_PASSWORD='<пароль SA>'; $env:JWT_SECRET_KEY='<ключ підпису JWT з Лаб. 09>'; $env:RABBITMQ_PASSWORD='<пароль>'; .\create-secrets.ps1
# Обов'язкові змінні оточення: SQL_PASSWORD, JWT_SECRET_KEY, RABBITMQ_PASSWORD (значень за замовчуванням немає навмисно).
foreach ($v in 'SQL_PASSWORD', 'JWT_SECRET_KEY', 'RABBITMQ_PASSWORD') {
    if (-not (Get-Item "env:$v" -ErrorAction SilentlyContinue)) { throw "Задайте змінну оточення $v" }
}
$SqlPassword = $env:SQL_PASSWORD; $JwtKey = $env:JWT_SECRET_KEY
$RabbitUser = 'platform'; $RabbitPass = $env:RABBITMQ_PASSWORD
$catalogDb = "Server=sql,1433;Database=CustomShop_Catalog;User Id=sa;Password=$SqlPassword;TrustServerCertificate=True;Encrypt=False"
$messaging = "amqp://${RabbitUser}:${RabbitPass}@rabbitmq:5672"

function Apply-Secret($name, [string[]]$literals) {
    kubectl create secret generic $name @literals --dry-run=client -o yaml | kubectl apply -f -
}

Apply-Secret 'sql-secrets'      @("--from-literal=MSSQL_SA_PASSWORD=$SqlPassword")
Apply-Secret 'rabbitmq-secrets' @("--from-literal=RABBITMQ_DEFAULT_USER=$RabbitUser", "--from-literal=RABBITMQ_DEFAULT_PASS=$RabbitPass")
Apply-Secret 'catalog-secrets'  @("--from-literal=ConnectionStrings__CatalogDb=$catalogDb",
                                  "--from-literal=ConnectionStrings__messaging=$messaging",
                                  "--from-literal=ConnectionStrings__redis=redis:6379",
                                  "--from-literal=Jwt__SecretKey=$JwtKey")
Apply-Secret 'gateway-secrets'  @("--from-literal=Jwt__SecretKey=$JwtKey")
