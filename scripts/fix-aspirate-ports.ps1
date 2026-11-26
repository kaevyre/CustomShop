# aspirate adds a bogus "https" port (a copy of the gRPC/HTTP port from launchSettings) to Services and Deployments
# of auth-api, catalog-api, orders-api, payments-api, reviews-api. kubectl rejects it ("duplicate entries for key [port=...]").
# Run after `aspirate generate` and before `aspirate apply`:   ./scripts/fix-aspirate-ports.ps1
$root = Join-Path $PSScriptRoot '..\Platform.AppHost\aspirate-output'
$enc = New-Object Text.UTF8Encoding $false
foreach ($s in 'auth-api','catalog-api','orders-api','payments-api','reviews-api') {
    foreach ($f in 'deployment.yaml','service.yaml') {
        $p = Join-Path $root "$s\$f"
        if (-not (Test-Path $p)) { continue }
        $t = [IO.File]::ReadAllText($p)
        $n = [regex]::Replace($t, '\r?\n[ ]*- name: https\r?\n[ ]*(?:containerPort|port): \d+(?:\r?\n[ ]*targetPort: \d+)?', '')
        if ($n -ne $t) { [IO.File]::WriteAllText($p, $n, $enc); Write-Host "fixed: $s/$f" }
    }
}
