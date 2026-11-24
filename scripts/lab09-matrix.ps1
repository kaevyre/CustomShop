# Лаб. 09: автоматична перевірка матриці прав Keycloak. Запуск:
#   . .\scripts\lab09-helpers.ps1
#   .\scripts\lab09-matrix.ps1 -ClientSecret "<orders.postman secret>" -Password "ChangeMe123!"
param(
  [Parameter(Mandatory)] [string] $ClientSecret,
  [string] $Password = "ChangeMe123!",
  [string] $ClientId = "orders.postman",
  [string] $Realm = "DevRealmDrivers"
)
$kc = "http://localhost:8080/realms/$Realm/protocol/openid-connect/token"
$id = 999999999
$calls = @(
  @("GET",    "/api/orders/$id"),            @("POST",   "/api/orders"),
  @("PUT",    "/api/orders/$id"),            @("DELETE", "/api/orders/$id"),
  @("POST",   "/api/orders/$id/approve"),
  @("GET",    "/api/products/$id"),          @("POST",   "/api/products"),
  @("POST",   "/api/products/$id/publish"),  @("DELETE", "/api/products/$id"),
  @("GET",    "/api/payments"),              @("POST",   "/api/payments/process"),
  @("POST",   "/api/payments/refund")
)
$rows = @()
foreach ($u in "alice.admin","john.manager","kate.user","greg.guest") {
  try {
    $tok = (Invoke-RestMethod $kc -Method Post -Body @{ grant_type="password"; client_id=$ClientId; client_secret=$ClientSecret; username=$u; password=$Password; scope="openid" }).access_token
  } catch { Write-Warning "token $u : $($_.Exception.Message)"; continue }
  foreach ($c in $calls) {
    $r = Call $c[0] "$gw$($c[1])" "{}" $tok
    $rows += [pscustomobject]@{ User=$u; Method=$c[0]; Path=$c[1]; Code=$r.Code; Allowed=($r.Code -ne 403 -and $r.Code -ne 401) }
  }
}
$rows | Format-Table -AutoSize
$rows | Export-Csv -NoTypeInformation -Encoding UTF8 docs\lab09\matrix-result.csv
"Saved: docs\lab09\matrix-result.csv"
