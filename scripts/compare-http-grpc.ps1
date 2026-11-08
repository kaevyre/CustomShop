# Порівняння затримок агрегувального запиту: HTTP REST (Лаб. 05) проти gRPC (Лаб. 06).
# Запуск при працюючому AppHost:  powershell -ExecutionPolicy Bypass -File scripts/compare-http-grpc.ps1
# Запити чергуються, щоб обидва транспорти були в однакових умовах; кеш агрегованих відповідей вимкнено (noCache=true).
param(
    [string]$BaseUrl = 'http://localhost:5000',
    [long]$OrderId = 1,
    [int]$Requests = 200,
    [int]$WarmUp = 20,
    [int]$Concurrency = 1      # скільки запитів іде одночасно в кожній пачці
)

$endpoints = [ordered]@{
    'HTTP REST' = "$BaseUrl/api/aggregator/orders/$OrderId/full/http"
    'gRPC'      = "$BaseUrl/api/aggregator/orders/$OrderId/full?noCache=true"
}

# один HttpClient із keep-alive: Invoke-WebRequest додає десятки мс власних накладних витрат і заглушує різницю
Add-Type -AssemblyName System.Net.Http
$client = [System.Net.Http.HttpClient]::new()

# Вимірює час пачки з $Concurrency одночасних запитів; GetAsync завершується після читання всього тіла відповіді.
function Measure-Request([string]$Url) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $tasks = [System.Threading.Tasks.Task[System.Net.Http.HttpResponseMessage][]](1..$Concurrency | ForEach-Object { $client.GetAsync($Url) })
    [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks)
    $elapsed = $sw.Elapsed.TotalMilliseconds
    foreach ($task in $tasks) {
        if (-not $task.Result.IsSuccessStatusCode) { throw "$Url -> $([int]$task.Result.StatusCode)" }
        $task.Result.Dispose()
    }
    return $elapsed
}

foreach ($url in $endpoints.Values) {
    1..$WarmUp | ForEach-Object { Measure-Request $url | Out-Null }   # JIT, пули з'єднань, кеші сервісів
}

$samples = @{}
foreach ($name in $endpoints.Keys) { $samples[$name] = [System.Collections.Generic.List[double]]::new() }

for ($i = 0; $i -lt $Requests; $i++) {
    foreach ($name in $endpoints.Keys) { $samples[$name].Add((Measure-Request $endpoints[$name])) }
}

function Get-Percentile([double[]]$Sorted, [double]$P) {
    return $Sorted[[Math]::Min([Math]::Ceiling($P * $Sorted.Length) - 1, $Sorted.Length - 1)]
}

$endpoints.Keys | ForEach-Object {
    $sorted = [double[]]($samples[$_] | Sort-Object)
    [pscustomobject]@{
        Transport  = $_
        Batches    = $sorted.Length
        Concurrent = $Concurrency
        'Avg, ms'  = [Math]::Round(($sorted | Measure-Object -Average).Average, 1)
        'P50, ms'  = [Math]::Round((Get-Percentile $sorted 0.50), 1)
        'P95, ms'  = [Math]::Round((Get-Percentile $sorted 0.95), 1)
    }
} | Format-Table -AutoSize
