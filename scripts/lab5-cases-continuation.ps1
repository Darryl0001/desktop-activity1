# Remaining cases use the helpers defined by verify-lab5.ps1.
function Batch([int]$student, [int[]]$equipmentIds, [int]$expected, [string]$date = $due) {
    Request POST '/api/v1/checkout-requests' $expected @{
        studentId = $student; equipmentIds = $equipmentIds; expectedReturnDate = $date
    }
}

Run-Case T05 {
    $pair = @($items['EQ-002'], $items['EQ-006'])
    $batch = Batch $s4 $pair 201
    Assert-That ($batch.Value.borrowings.Count -eq 2) 'Batch did not return two created records'
    Assert-That (($batch.After.borrowings | Where-Object StudentId -eq $s4).Count -eq 2) 'Batch did not persist two rows'
    foreach ($id in $pair) { Assert-That (!($batch.After.equipment | Where-Object Id -eq $id).IsAvailable) 'Batch left an issued item available' }

    Reset-Fixture
    $boundary = Request POST '/api/v1/borrowings' 201 @{ studentId = $s3; equipmentId = $items['EQ-002']; expectedReturnDate = $due }
    Assert-That (($boundary.After.borrowings | Where-Object { $_.StudentId -eq $s3 -and $_.Status -eq 0 }).Count -eq 3) 'Single checkout did not reach exactly three'
    Assert-That (!($boundary.After.equipment | Where-Object Identifier -eq 'EQ-002').IsAvailable) 'Single checkout did not change availability'
    Reset-Fixture
    $batch = Batch $s1 $pair 201
    Assert-That (($batch.After.borrowings | Where-Object { $_.StudentId -eq $s1 -and $_.Status -eq 0 }).Count -eq 3) 'Batch checkout did not reach exactly three'
    foreach ($id in $pair) { Assert-That (!($batch.After.equipment | Where-Object Id -eq $id).IsAvailable) 'Batch boundary left an issued item available' }
    Reset-Fixture
    $over = Batch $s3 $pair 409
    Assert-That ($over.Value.message -like '*limit*') 'The over-limit checkout returned the wrong rejection'
    Reset-Fixture
    $null = Batch $s4 @($pair[0], $pair[0]) 400
    $null = Batch $s4 @($pair[0]) 400
    $null = Batch $s4 @($items['EQ-001'], $pair[0]) 409
    $null = Batch $s2 $pair 409
    $null = Batch $s4 @($pair[0], $missing) 404
    $null = Batch $s4 $pair 400 'not-a-date'

    Reset-Fixture
    Sql @('-f', (Join-Path $PSScriptRoot 'lab5-inject-rollback.sql')) | Out-Null
    try {
        $fault = Batch $s4 $pair 500
        Assert-That ($fault.Value.code -eq 'server_error' -and $fault.Raw -notmatch 'P0001|StackTrace|rollback fault') 'Injected fault leaked server detail'
        $lab5FaultLine = Select-String -LiteralPath $pgLog -Pattern 'LAB5_T05 observed the first borrowing' | Select-Object -Last 1
        Assert-That ($null -ne $lab5FaultLine) 'PostgreSQL did not confirm that the first insert had occurred'
        Trace @{ event = 'controlled fault after first insert'; postgresLog = $lab5FaultLine.Line.Trim() }
    } finally {
        Sql @('-c', 'DROP FUNCTION lab5_reject_second_insert() CASCADE') | Out-Null
    }
}

Run-Case T06 {
    $before = Snapshot
    $history = (Request GET "/api/v1/students/$s1/borrowings" 200).Value
    Assert-That (($history.status -join ',') -eq 'Active,Returned') 'Student history must include both statuses'
    Assert-That ((Request GET "/api/v1/students/$s4/borrowings" 200).Raw -eq '[]') 'Student history should be empty'
    foreach ($code in @('EQ-001', 'EQ-003')) {
        $history = (Request GET "/api/v1/equipment/$($items[$code])/borrowings" 200).Value
        Assert-That ($history.Count -eq 1 -and $history[0].equipmentCode -eq $code) 'Equipment history contained the wrong subject'
    }
    $null = Request GET "/api/v1/students/$missing/borrowings" 404
    $null = Request GET "/api/v1/equipment/$missing/borrowings" 404

    foreach ($probe in @(@('%20aDA%20', 2), @('st-30', 2), @('PrOjE', 1))) {
        $matches = (Request GET "/api/v1/borrowings?search=$($probe[0])" 200).Value
        Assert-That ($matches.Count -eq $probe[1]) "Incorrect partial search for $($probe[0])"
    }
    foreach ($probe in @(@('Active', 3), @('Returned', 1), @('All', 4))) {
        $rows = (Request GET "/api/v1/borrowings?status=$($probe[0])" 200).Value
        Assert-That ($rows.Count -eq $probe[1]) "Incorrect row count for $($probe[0])"
        if ($probe[0] -ne 'All') { Assert-That (!($rows | Where-Object status -ne $probe[0])) 'Status filter included another status' }
    }

    $default = (Request GET '/api/v1/borrowings' 200).Value
    foreach ($field in @('borrowedDate', 'expectedReturnDate')) {
        foreach ($direction in @('asc', 'desc')) {
            $rows = (Request GET "/api/v1/borrowings?sort=$field&direction=$direction" 200).Value
            $expectedIds = ($default | Sort-Object @{ Expression = { $_[$field] }; Descending = ($direction -eq 'desc') }, id).id -join ','
            Assert-That (($rows.id -join ',') -eq $expectedIds) "Incorrect $field $direction order or tie-break"
        }
    }
    $combined = (Request GET '/api/v1/borrowings?search=aDa&status=Returned&sort=expectedReturnDate&direction=desc' 200).Value
    Assert-That ($combined.Count -eq 1 -and $combined[0].equipmentCode -eq 'EQ-003') 'Combined criteria produced the wrong record'
    $clear = (Request GET '/api/v1/borrowings?status=All&sort=borrowedDate&direction=asc' 200).Value
    Assert-That (($clear.id -join ',') -eq ($default.id -join ',')) 'Clear did not restore default criteria'
    Assert-That ((Request GET '/api/v1/borrowings?search=does-not-exist' 200).Raw -eq '[]') 'Expected [] for no matches'
    Assert-That ((Request GET '/api/v1/borrowings?search=%25' 200).Raw -eq '[]') 'Percent must be literal search text'
    $null = Request GET '/api/v1/borrowings?status=Overdue' 400
    $null = Request GET '/api/v1/borrowings?direction=down' 400
    $null = Request GET '/api/v1/borrowings?sort=returnedDate' 400
    $after = Snapshot
    Same-State $before $after 'History reads changed PostgreSQL'
    Trace @{ event = 'History queries preserved PostgreSQL state'; before = $before; after = $after }
}

Run-Case T07 {
    $null = Request GET '/api/v1/equipment?available=true' 200
    Run-Desktop HttpWorkflows
    $state = Snapshot
    $student = $state.students | Where-Object StudentNumber -eq 'ST-UI'
    $item = $state.equipment | Where-Object Identifier -eq 'EQ-UI'
    Assert-That ($student.Name -eq 'Changed by independent HTTP client' -and $item.Name -eq 'Edited Desktop Kit') 'Desktop management changes did not persist'
    $loan = $state.borrowings | Where-Object { $_.StudentId -eq $student.Id -and $_.EquipmentId -eq $item.Id }
    Assert-That ($loan.Status -eq 1 -and $item.IsAvailable) 'Desktop issue and return were not persisted'
    Trace @{ event = 'PostgreSQL after real Avalonia HTTP workflows'; state = $state }

    $assetsFile = Join-Path $repo 'EquipmentBorrowing.Desktop/obj/project.assets.json'
    $assets = Parse ([IO.File]::ReadAllText($assetsFile))
    $packages = @($assets.libraries.Keys | Sort-Object)
    Assert-That (!($packages | Where-Object { $_ -match 'Npgsql|EntityFrameworkCore|EquipmentBorrowing.Infrastructure|EquipmentBorrowing.Api' })) 'Desktop has a persistence or API project dependency'
    $packages | Set-Content -LiteralPath (Join-Path $evidence 'T07-desktop-dependencies.txt') -Encoding utf8
}

Run-Case T08 {
    $issue = Request POST '/api/v1/borrowings' 201 @{ studentId = $s4; equipmentId = $items['EQ-002']; expectedReturnDate = $due }
    $saved = $issue.After
    Stop-Api
    $null = Request GET '/api/v1/equipment' 0
    Run-Desktop ApiFailureFeedback unreachable

    Start-Api $true
    $failure = Request GET '/api/v1/equipment' 500
    Assert-That ($failure.Value.code -eq 'server_error' -and $failure.Raw -notmatch 'Npgsql|localhost|stack|Username|Password') 'Server error was not safe'
    $null = Request POST '/api/v1/students' 500 @{ fullName = 'Must not persist'; studentNumber = 'ST-FAIL' }
    Run-Desktop ApiFailureFeedback server
    $safeLog = @(Select-String -LiteralPath (Join-Path $cluster "api-$script:restartCount.log") -Pattern 'An unhandled exception has occurred while executing the request' |
        ForEach-Object { $_.Line.Trim() })
    Assert-That ($safeLog.Count -gt 0) 'Server diagnostics were missing'
    $safeLog | Set-Content -LiteralPath (Join-Path $evidence 'T08-safe-server-log.txt') -Encoding utf8

    Stop-Api
    Restart-Postgres
    Start-Api
    $read = Request GET $issue.Location 200
    Assert-That ($read.Value.status -eq 'Active') 'Previously committed data was lost'
    Run-Desktop ApiFailureFeedback recovered
    Same-State $saved (Snapshot) 'Known failed operations or restart changed state'
    Trace @{ event = 'PostgreSQL after API and DB recovery'; state = (Snapshot) }

    Run-Desktop UnknownWriteResponseRequiresReview lost-write-response
    $afterLoss = Snapshot
    $persisted = @($afterLoss.students | Where-Object StudentNumber -eq 'ST-LOST')
    Assert-That ($persisted.Count -eq 1) 'Exactly one committed student should exist after response loss'
    $writes = @(Get-Content -LiteralPath (Join-Path $evidence 'T08-lost-write-response-desktop.jsonl') |
        ForEach-Object { Parse $_ } | Where-Object method -eq 'POST')
    Assert-That ($writes.Count -eq 1 -and $writes[0].status -eq 201 -and $writes[0].responseDropped) 'The client retried or the test did not drop the response'
    Trace @{ event = 'Fresh PostgreSQL read after committed write response was dropped'; state = $afterLoss }
}
