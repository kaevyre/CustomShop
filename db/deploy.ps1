<#
=====================================================================
 Розгортання трьох баз CustomShop з нуля (Windows PowerShell 5.1 / PowerShell 7).

 Використання (з кореня репозиторію):
   .\db\deploy.ps1                       усі три бази, локальні sqlcmd і mongosh
   .\db\deploy.ps1 -Docker               усі три бази в контейнерах з db\docker-compose.yml
   .\db\deploy.ps1 -Projects p1,p3       лише вибрані проєкти
   .\db\deploy.ps1 -Checks               після розгортання виконати checks.sql / queries.js
   .\db\deploy.ps1 -SeedOnly             лише повторити seed (перевірка ідемпотентності)
   .\db\deploy.ps1 -Trusted              Windows-автентифікація в SQL Server (замість sa)

 Якщо PowerShell блокує запуск скриптів:
   powershell -ExecutionPolicy Bypass -File .\db\deploy.ps1 -Docker

 Порядок:  p1: schema -> procedures -> seed
           p2: schema -> seed
           p3: collections -> seed
 Увага: schema.sql перестворює таблиці (наявні дані видаляються).
=====================================================================
#>
[CmdletBinding()]
param(
    [ValidateSet('p1', 'p2', 'p3')]
    [string[]] $Projects = @('p1', 'p2', 'p3'),
    [switch]   $Docker,
    [switch]   $Checks,
    [switch]   $SeedOnly,
    [switch]   $Trusted,
    [string]   $SqlServer   = $(if ($env:SQL_SERVER)   { $env:SQL_SERVER }   else { 'localhost' }),
    [string]   $SqlUser     = $(if ($env:SQL_USER)     { $env:SQL_USER }     else { 'sa' }),
    [string]   $SqlPassword = $(if ($env:SQL_PASSWORD) { $env:SQL_PASSWORD } else { 'Str0ng!Passw0rd' }),
    [string]   $MongoUri    = $(if ($env:MONGO_URI)    { $env:MONGO_URI }    else { 'mongodb://localhost:27017' })
)

$ErrorActionPreference = 'Stop'
$DbDir = $PSScriptRoot

function Write-Step([string] $Text) {
    Write-Host ""
    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Assert-ExitCode([string] $What) {
    if ($LASTEXITCODE -ne 0) { throw "Помилка під час виконання: $What (код $LASTEXITCODE)" }
}

function Invoke-SqlFile([string] $File) {
    Write-Step "SQL   $File"
    if ($Docker) {
        docker exec customshop-sql /opt/mssql-tools18/bin/sqlcmd `
            -S localhost -U sa -P $SqlPassword -C -I -f 65001 -b -i "/db/$File"
    }
    else {
        $path = Join-Path $DbDir $File
        if ($Trusted) {
            sqlcmd -S $SqlServer -E -C -I -f 65001 -b -i $path
        }
        else {
            sqlcmd -S $SqlServer -U $SqlUser -P $SqlPassword -C -I -f 65001 -b -i $path
        }
    }
    Assert-ExitCode $File
}

function Invoke-MongoFile([string] $File) {
    Write-Step "MONGO $File"
    if ($Docker) {
        docker exec customshop-mongo mongosh --quiet "/db/$File"
    }
    else {
        mongosh --quiet $MongoUri (Join-Path $DbDir $File)
    }
    Assert-ExitCode $File
}

# ---------- підготовка ----------
if ($Docker) {
    Write-Step "Запуск контейнерів (db\docker-compose.yml)"
    $env:SQL_PASSWORD = $SqlPassword
    docker compose -f (Join-Path $DbDir 'docker-compose.yml') up -d
    Assert-ExitCode 'docker compose up'

    Write-Host -NoNewline 'Очікування готовності SQL Server і MongoDB'
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        $sql   = docker inspect -f '{{.State.Health.Status}}' customshop-sql   2>$null
        $mongo = docker inspect -f '{{.State.Health.Status}}' customshop-mongo 2>$null
        if ($sql -eq 'healthy' -and $mongo -eq 'healthy') { $ready = $true; break }
        Write-Host -NoNewline '.'
        Start-Sleep -Seconds 3
    }
    Write-Host ''
    if (-not $ready) { throw 'Контейнери не стали healthy. Перевірте: docker compose -f db\docker-compose.yml logs' }
}
else {
    if (($Projects -contains 'p1' -or $Projects -contains 'p2') -and -not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        throw 'sqlcmd не знайдено. Встановіть його (winget install sqlcmd) або запустіть з -Docker'
    }
    if ($Projects -contains 'p3' -and -not (Get-Command mongosh -ErrorAction SilentlyContinue)) {
        throw 'mongosh не знайдено. Встановіть його (winget install MongoDB.Shell) або запустіть з -Docker'
    }
}

# ---------- розгортання ----------
foreach ($p in $Projects) {
    switch ($p) {
        'p1' {
            if (-not $SeedOnly) {
                Invoke-SqlFile 'p1/schema.sql'
                Invoke-SqlFile 'p1/procedures.sql'
            }
            Invoke-SqlFile 'p1/seed.sql'
            if ($Checks) { Invoke-SqlFile 'p1/checks.sql' }
        }
        'p2' {
            if (-not $SeedOnly) { Invoke-SqlFile 'p2/schema.sql' }
            Invoke-SqlFile 'p2/seed.sql'
            if ($Checks) { Invoke-SqlFile 'p2/checks.sql' }
        }
        'p3' {
            if (-not $SeedOnly) { Invoke-MongoFile 'p3/collections.js' }
            Invoke-MongoFile 'p3/seed.js'
            if ($Checks) { Invoke-MongoFile 'p3/queries.js' }
        }
    }
}

Write-Step "Готово: $($Projects -join ', ')"
