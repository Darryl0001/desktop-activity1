#Requires -Version 7.5
param(
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin',
    [string]$EvidenceDir
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$EvidenceDir) { $EvidenceDir = Join-Path $repo '.local/lab5-evidence' }
$evidence = [IO.Path]::GetFullPath($EvidenceDir)
$cluster = Join-Path $repo ('.local/lab5-postgres-' + [Guid]::NewGuid().ToString('N'))
$dataDir = Join-Path $cluster 'data'
$pgLog = Join-Path $cluster 'postgres.log'
$dbName = 'lab5_acceptance'
$apiProcess = $null
$pgStarted = $false
$phase = 'setup'
$restartCount = 0
$results = [System.Collections.Generic.List[object]]::new()

function Check-Exit([string]$step) {
    if ($LASTEXITCODE -ne 0) { throw "$step failed with exit code $LASTEXITCODE" }
}

function Port {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}

function Assert-That([bool]$condition, [string]$detail) {
    if (!$condition) { throw $detail }
}

function Pg([string]$binary, [string[]]$arguments) {
    & (Join-Path $PostgresBin $binary) @arguments
    Check-Exit $binary
}

function Sql([string[]]$arguments) {
    Pg 'psql.exe' (@('-X', '-qAt', '-h', '127.0.0.1', '-p', $pgPort, '-U', 'lab5_test', '-d', $dbName,
        '-v', 'ON_ERROR_STOP=1') + $arguments)
}

function Parse([string]$json) {
    ConvertFrom-Json -InputObject $json -NoEnumerate -AsHashtable -DateKind String
}

function Trace($entry) {
    $entry.timestamp = [DateTimeOffset]::UtcNow.ToString('o')
    $entry.test = $script:phase
    $entry | ConvertTo-Json -Compress -Depth 25 | Add-Content -LiteralPath (Join-Path $evidence 'http-and-postgres.jsonl') -Encoding utf8
}

function Snapshot {
    $lines = Sql @('-f', (Join-Path $PSScriptRoot 'lab5-snapshot.sql'))
    Parse ($lines -join "`n")
}

function Same-State($before, $after, [string]$description) {
    Assert-That (($before | ConvertTo-Json -Compress -Depth 15) -ceq ($after | ConvertTo-Json -Compress -Depth 15)) $description
}

function Request([string]$method, [string]$path, [int]$expected, $body = $null, [hashtable]$headers = @{}) {
    $uri = $baseUrl.TrimEnd('/') + $path
    $mutates = $method -cin @('POST', 'PUT', 'PATCH')
    $before = if ($mutates) { Snapshot }
    $parameters = @{ Uri = $uri; Method = $method; TimeoutSec = 7; SkipHttpErrorCheck = $true; Headers = $headers }
    if ($null -ne $body) {
        $parameters.ContentType = 'application/json; charset=utf-8'
        $parameters.Body = if ($body -is [string]) { $body } else { $body | ConvertTo-Json -Compress -Depth 8 }
    }

    try { $wire = Invoke-WebRequest @parameters }
    catch {
        Trace @{ method = $method; path = $path; request = $parameters.Body; transport = $_.Exception.GetType().Name; detail = $_.Exception.Message }
        if ($expected -eq 0) { return @{ Status = 0 } }
        throw
    }

    $status = [int]$wire.StatusCode
    $content = [string]$wire.Content
    $after = if ($mutates) { Snapshot }
    $location = $wire.Headers['Location'] | Select-Object -First 1
    $etag = $wire.Headers['ETag'] | Select-Object -First 1
    $allow = $wire.Headers['Allow'] -join ', '
    $trace = @{ method = $method; path = $path; request = $parameters.Body; status = $status; response = $content }
    if ($location) { $trace.location = $location }
    if ($etag) { $trace.etag = $etag }
    if ($allow) { $trace.allow = $allow }
    if ($mutates) { $trace.before = $before; $trace.after = $after }
    if ($path -eq '/openapi/v1.json') { $trace.response = 'See openapi-served.json' }
    Trace $trace

    Assert-That ($status -eq $expected) "$method $path expected $expected but received ${status}: $content"
    $value = Parse $content
    if ($status -ge 400) {
        Assert-That ($value.code -and $value.message) "$method $path returned an incomplete error body"
        if ($mutates) { Same-State $before $after "$method $path changed data on a known failure" }
    }
    @{ Status = $status; Value = $value; Location = $location; ETag = $etag; Allow = $allow; Before = $before; After = $after; Raw = $content }
}

function Reset-Fixture {
    Sql @('-f', (Join-Path $PSScriptRoot 'lab5-reset.sql')) | Out-Null
    Trace @{ event = 'fixture reset'; localDate = $env:Lab5__FixedDate; state = (Snapshot) }
    $students = (Request GET '/api/v1/students' 200).Value
    $equipment = (Request GET '/api/v1/equipment' 200).Value
    $loans = (Request GET '/api/v1/borrowings' 200).Value
    $script:s1 = ($students | Where-Object studentNumber -eq 'ST-100').id
    $script:s2 = ($students | Where-Object studentNumber -eq 'ST-200').id
    $script:s3 = ($students | Where-Object studentNumber -eq 'ST-300').id
    $script:s4 = ($students | Where-Object studentNumber -eq 'ST-400').id
    $script:items = @{}
    foreach ($row in $equipment) { $script:items[$row.equipmentCode] = $row.id }
    $script:e1Loan = ($loans | Where-Object equipmentCode -eq 'EQ-001').id
}

function Restart-Postgres {
    Pg 'pg_ctl.exe' @('-D', $dataDir, '-l', $pgLog, '-w', 'restart')
}

function Stop-Api {
    if ($script:apiProcess) {
        if (!$script:apiProcess.HasExited) { Stop-Process -Id $script:apiProcess.Id -Force; $script:apiProcess.WaitForExit() }
        $script:apiProcess.Dispose()
        $script:apiProcess = $null
    }
}

function Start-Api([bool]$badDatabase = $false) {
    Stop-Api
    $script:restartCount++
    $env:ConnectionStrings__EquipmentBorrowing = if ($badDatabase) { "$connection;Database=lab5_intentionally_missing" } else { $connection }
    $apiDll = Join-Path $repo 'src/EquipmentBorrowing.Api/bin/Debug/net10.0/EquipmentBorrowing.Api.dll'
    $script:apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @("`"$apiDll`"", '--urls', $baseUrl.TrimEnd('/')) `
        -WorkingDirectory $repo -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $cluster "api-$script:restartCount.log") `
        -RedirectStandardError (Join-Path $cluster "api-$script:restartCount.stderr.log")
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($script:apiProcess.HasExited) { throw "API exited during startup. See $cluster." }
        try {
            $probe = Invoke-WebRequest -Uri ($baseUrl + 'openapi/v1.json') -TimeoutSec 2
            if ($probe.StatusCode -eq 200) {
                Trace @{ event = 'API started'; badDatabase = $badDatabase }
                return
            }
        } catch { Start-Sleep -Milliseconds 120 }
    }
    throw 'API did not become ready.'
}

function Run-Desktop([string]$test, [string]$failure = '') {
    $env:LAB5_TEST_BASE_URL = $baseUrl
    $env:LAB5_TEST_CONNECTION_STRING = $connection + ';Pooling=false'
    $env:LAB5_UI_EVIDENCE_DIR = Join-Path $evidence 'ui'
    $env:LAB5_SNAPSHOT_SQL = Join-Path $PSScriptRoot 'lab5-snapshot.sql'
    $env:LAB5_UI_FAILURE = $failure
    $name = if ($failure) { "T08-$failure" } else { 'T07' }
    $env:LAB5_UI_TRACE_FILE = Join-Path $evidence "$name-desktop.jsonl"
    [IO.File]::WriteAllText($env:LAB5_UI_TRACE_FILE, '')
    dotnet test tests/EquipmentBorrowing.Tests --no-build --filter "FullyQualifiedName~Lab5DesktopAcceptance.$test" `
        --verbosity minimal 2>&1 | Tee-Object -FilePath (Join-Path $evidence "$name-desktop-run.txt") | Out-Host
    Check-Exit "Avalonia $test $failure"
    Assert-That ((Get-Item -LiteralPath $env:LAB5_UI_TRACE_FILE).Length -gt 0) 'Avalonia test was not executed'
    Remove-Item Env:LAB5_TEST_BASE_URL
    Remove-Item Env:LAB5_UI_FAILURE -ErrorAction SilentlyContinue
}

function Run-Case([string]$id, [scriptblock]$steps) {
    $script:phase = $id
    $start = [DateTimeOffset]::UtcNow.ToString('o')
    try {
        Reset-Fixture
        & $steps
        $results.Add(@{ id = $id; status = 'Pass'; started = $start })
        Write-Host "PASS $id"
    } catch {
        $results.Add(@{ id = $id; status = 'Fail'; started = $start; diagnosis = $_.Exception.Message })
        Write-Host "FAIL ${id}: $($_.Exception.Message)"
    }
}

$prior = @{}
foreach ($name in @('LAB4_CONNECTION_STRING', 'LAB4_TEST_CONNECTION_STRING', 'ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT',
    'ConnectionStrings__EquipmentBorrowing', 'Lab5__FixedDate', 'LAB5_TEST_CONNECTION_STRING', 'LAB5_TEST_BASE_URL',
    'LAB5_UI_EVIDENCE_DIR', 'LAB5_UI_FAILURE', 'LAB5_SNAPSHOT_SQL', 'LAB5_UI_TRACE_FILE')) {
    $prior[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

Push-Location $repo
try {
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    [IO.Directory]::CreateDirectory($cluster) | Out-Null
    [IO.File]::WriteAllText((Join-Path $evidence 'http-and-postgres.jsonl'), '')
    $pgPort = Port
    do { $apiPort = Port } while ($apiPort -eq $pgPort)
    $baseUrl = "http://127.0.0.1:$apiPort/"
    $connection = "Host=127.0.0.1;Port=$pgPort;Username=lab5_test;Timeout=3;Command Timeout=6;Database=$dbName"
    $env:ASPNETCORE_ENVIRONMENT = 'Testing'
    $env:DOTNET_ENVIRONMENT = 'Testing'
    $env:Lab5__FixedDate = '2026-11-10'
    $env:LAB4_CONNECTION_STRING = $connection

    Pg 'initdb.exe' @('-D', $dataDir, '-U', 'lab5_test', '-A', 'trust', '--encoding=UTF8', '--locale=C') |
        Set-Content -LiteralPath (Join-Path $cluster 'initdb.log')
    Pg 'pg_ctl.exe' @('-D', $dataDir, '-l', $pgLog, '-o', "-h 127.0.0.1 -p $pgPort", '-w', 'start')
    $pgStarted = $true
    Pg 'createdb.exe' @('-h', '127.0.0.1', '-p', $pgPort, '-U', 'lab5_test', $dbName)

    dotnet build EquipmentBorrowing.slnx -p:NuGetAudit=false --verbosity minimal
    Check-Exit 'Build Lab 5'
    dotnet run --no-build --project src/EquipmentBorrowing.ConsoleApp -- migrate
    Check-Exit 'Apply Lab 4 migrations'
    Start-Api
    . (Join-Path $PSScriptRoot 'lab5-cases.ps1')

    $env:LAB4_TEST_CONNECTION_STRING = "Host=127.0.0.1;Port=$pgPort;Database=postgres;Username=lab5_test;Timeout=3"
    dotnet test tests/EquipmentBorrowing.Tests --no-build --filter 'FullyQualifiedName!~Lab5DesktopAcceptance' --verbosity minimal 2>&1 |
        Tee-Object -FilePath (Join-Path $evidence 'lab4-regression-run.txt') | Out-Host
    $regressionPassed = $LASTEXITCODE -eq 0
    $results | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $evidence 'run-results.json') -Encoding utf8
    if ($results.Count -ne 8 -or ($results | Where-Object { $_.status -ne 'Pass' }) -or !$regressionPassed) {
        throw "Verification had failures. See $evidence and $cluster."
    }
    Write-Host "T01–T08 and Lab 4 regressions passed. Evidence at $evidence."
}
finally {
    Stop-Api
    if ($pgStarted) { Pg 'pg_ctl.exe' @('-D', $dataDir, '-m', 'fast', '-w', 'stop') }
    foreach ($name in $prior.Keys) { [Environment]::SetEnvironmentVariable($name, $prior[$name], 'Process') }
    Pop-Location
}
