param(
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin',
    [ValidateRange(1024, 65535)][int]$Port = 55485
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cluster = [IO.Path]::GetFullPath((Join-Path $workspace ('.local/verify-' + [Guid]::NewGuid().ToString('N'))))
if (!$cluster.StartsWith((Join-Path $workspace '.local'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected verification path.' }
$evidence = Join-Path $workspace 'docs/evidence'
New-Item -ItemType Directory -Path $cluster, $evidence -Force | Out-Null
foreach ($binary in @('initdb.exe','pg_ctl.exe','createdb.exe')) {
    if (!(Test-Path -LiteralPath (Join-Path $PostgresBin $binary))) { throw "Missing PostgreSQL binary: $binary" }
}
$previousTest = $env:LAB4_TEST_CONNECTION_STRING
$previousApp = $env:LAB4_CONNECTION_STRING
$previousProbe = $env:LAB4_VERIFY_DISPOSABLE
$previousImages = $env:LAB4_UI_EVIDENCE_DIR
$started = $false
function Check-Exit([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed with exit code $LASTEXITCODE" } }
function Run-Probe([string]$phase) {
    dotnet run --no-build --project (Join-Path $workspace 'tests/EquipmentBorrowing.PersistenceProbe') -- $phase |
        Tee-Object -FilePath (Join-Path $evidence 'restart-readback.txt') -Append
    Check-Exit "Persistence probe $phase"
}
Push-Location $workspace
try {
    & (Join-Path $PostgresBin 'initdb.exe') -D (Join-Path $cluster 'data') -U lab4_test -A trust --encoding=UTF8 --locale=C > (Join-Path $cluster 'initdb.log')
    Check-Exit 'Initialize disposable cluster'
    & (Join-Path $PostgresBin 'pg_ctl.exe') -D (Join-Path $cluster 'data') -l (Join-Path $cluster 'postgres.log') -o "-h 127.0.0.1 -p $Port" -w start
    Check-Exit 'Start disposable cluster'
    $started = $true
    $env:LAB4_TEST_CONNECTION_STRING = "Host=127.0.0.1;Port=$Port;Database=postgres;Username=lab4_test;Timeout=3"
    $env:LAB4_UI_EVIDENCE_DIR = Join-Path $evidence 'ui'
    dotnet build EquipmentBorrowing.slnx --verbosity quiet
    Check-Exit 'Build'
    dotnet test EquipmentBorrowing.slnx --no-build --results-directory $evidence --logger 'trx;LogFileName=lab4-postgres.trx' --verbosity minimal
    Check-Exit 'PostgreSQL and UI tests'
    & (Join-Path $PostgresBin 'createdb.exe') -h 127.0.0.1 -p $Port -U lab4_test lab4_restart_probe
    Check-Exit 'Create restart database'
    $env:LAB4_CONNECTION_STRING = "Host=127.0.0.1;Port=$Port;Database=lab4_restart_probe;Username=lab4_test;Timeout=3"
    $env:LAB4_VERIFY_DISPOSABLE = '1'
    dotnet run --no-build --project src/EquipmentBorrowing.ConsoleApp -- migrate
    Check-Exit 'Apply migrations'
    dotnet run --no-build --project src/EquipmentBorrowing.ConsoleApp -- seed
    Check-Exit 'Seed'
    dotnet build tests/EquipmentBorrowing.PersistenceProbe --verbosity quiet
    Check-Exit 'Build restart probe'
    [IO.File]::WriteAllText((Join-Path $evidence 'restart-readback.txt'), '')
        Run-Probe 'issue'
        & (Join-Path $PostgresBin 'pg_ctl.exe') -D (Join-Path $cluster 'data') -l (Join-Path $cluster 'postgres.log') -w restart
        Check-Exit 'First server restart'
        Run-Probe 'read'
        Run-Probe 'return'
        & (Join-Path $PostgresBin 'pg_ctl.exe') -D (Join-Path $cluster 'data') -l (Join-Path $cluster 'postgres.log') -w restart
        Check-Exit 'Second server restart'
        Run-Probe 'verify-return'
    Write-Output 'PASS: build, PostgreSQL tests, UI render tests, and separate-process/server restart persistence.'
}
finally {
    if ($started) { & (Join-Path $PostgresBin 'pg_ctl.exe') -D (Join-Path $cluster 'data') -m fast -w stop }
    $env:LAB4_TEST_CONNECTION_STRING = $previousTest
    $env:LAB4_CONNECTION_STRING = $previousApp
    $env:LAB4_VERIFY_DISPOSABLE = $previousProbe
    $env:LAB4_UI_EVIDENCE_DIR = $previousImages
    Pop-Location
}
