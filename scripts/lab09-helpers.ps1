# Лаб. 09: допоміжні функції для перевірки. Підключення:  . .\scripts\lab09-helpers.ps1
# Сумісно з Windows PowerShell 5.1 і PowerShell 7.
$global:gw = "http://localhost:5000"

function global:Call($method, $url, $body = $null, $token = $null, $session = $null) {
  $p = @{ Uri=$url; Method=$method; UseBasicParsing=$true; ContentType="application/json" }
  if ($body)    { $p.Body = $body }
  if ($token)   { $p.Headers = @{ Authorization = "Bearer $token" } }
  if ($session) { $p.WebSession = $session }
  try { $r = Invoke-WebRequest @p; $code = [int]$r.StatusCode; $text = $r.Content }
  catch {
    $resp = $_.Exception.Response
    if ($resp) {
      $code = [int]$resp.StatusCode
      $text = (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd()
    } else { $code = -1; $text = $_.Exception.Message }
  }
  [pscustomobject]@{ Code=$code; Body=$text }
}

function global:Get-LocalToken($user, $password) {
  $r = Call POST "$gw/auth/login" (@{ usernameOrEmail=$user; password=$password } | ConvertTo-Json)
  if ($r.Code -ne 200) { Write-Warning "login $user -> $($r.Code) $($r.Body)"; return $null }
  ($r.Body | ConvertFrom-Json).accessToken
}

function global:Decode-Jwt($t) {
  $p = $t.Split('.')[1].Replace('-','+').Replace('_','/')
  $p += '=' * ((4 - $p.Length % 4) % 4)
  [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json | ConvertTo-Json -Depth 6
}
