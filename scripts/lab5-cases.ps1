# Dot-sourced by verify-lab5.ps1 after the API and disposable database start.
$missing = 2147483647
$due = '2026-11-12'

function OpenApi-Check {
    $response = Request GET '/openapi/v1.json' 200
    [IO.File]::WriteAllText((Join-Path $evidence 'openapi-served.json'), $response.Raw)
    $document = $response.Value
    Assert-That ($document.openapi -like '3.*') 'OpenAPI 3 description is missing'
    $catalog = @(
        'get equipment Equipment[] - 200,400,500 Available'
        'get students Student[] - 200,400,500 Search'
        'get students/{id} Student - 200,400,404,500 ResourceId'
        'post students Student StudentCreate 201,400,409,500 -'
        'put students/{id} Student StudentUpdate 200,400,404,409,500 ResourceId,IfMatch'
        'patch students/{id}/eligibility Student Eligibility 200,400,404,409,500 ResourceId,IfMatch'
        'get equipment/{id} Equipment - 200,400,404,500 ResourceId'
        'post equipment Equipment EquipmentCreate 201,400,409,500 -'
        'put equipment/{id} Equipment EquipmentUpdate 200,400,404,409,500 ResourceId,IfMatch'
        'post borrowings Borrowing BorrowingCreate 201,400,404,409,500 -'
        'post checkout-requests CheckoutResult CheckoutCreate 201,400,404,409,500 -'
        'post borrowings/{id}/return Borrowing Return 200,400,404,409,500 ResourceId'
        'get borrowings/{id} Borrowing - 200,400,404,500 ResourceId'
        'get borrowings Borrowing[] - 200,400,500 Search,Status,Sort,Direction'
        'get students/{id}/borrowings Borrowing[] - 200,400,404,500 ResourceId'
        'get equipment/{id}/borrowings Borrowing[] - 200,400,404,500 ResourceId'
    )
    $operationCount = 0
    foreach ($route in $document.paths.Values) { $operationCount += $route.Count }
    Assert-That ($operationCount -eq $catalog.Count) 'OpenAPI must describe all 16 operations'
    for ($index = 0; $index -lt $catalog.Count; $index++) {
        $method, $route, $output, $inputSchema, $status, $params = $catalog[$index] -split ' '
        $number = 'API-{0:00}' -f ($index + 1)
        $operation = $document.paths["/api/v1/$route"][$method]
        Assert-That ($operation.operationId -eq $number) "Missing OpenAPI operation $number"
        Assert-That ((($operation.responses.Keys | Sort-Object) -join ',') -eq $status) "$number status codes differ"
        $success = $operation.responses[($status -split ',')[0]].content['application/json'].schema
        if ($output.EndsWith('[]')) {
            Assert-That ($success.type -eq 'array') "$number must describe an array response"
            $success = $success.items
        }
        Assert-That (($success['$ref'] -split '/')[-1] -eq $output.TrimEnd('[', ']')) "$number response schema differs"
        if ($inputSchema -ne '-') {
            $body = $operation.requestBody.content['application/json'].schema
            Assert-That (($body['$ref'] -split '/')[-1] -eq $inputSchema) "$number request schema differs"
        }
        if ($params -ne '-') {
            $names = ($operation.parameters | ForEach-Object { ($_['$ref'] -split '/')[-1] }) -join ','
            Assert-That ($names -eq $params) "$number parameter schema differs"
        }
    }
    Assert-That ($document.components.schemas.Borrowing.properties.expectedReturnDate.format -eq 'date') 'Dates must be documented'
    Assert-That ($document.components.schemas.CheckoutCreate.properties.equipmentIds.uniqueItems) 'Distinct items must be documented'
}

Run-Case T01 {
    $before = Snapshot
    $all = Request GET '/api/v1/equipment' 200
    Assert-That ($all.Value.Count -eq 7) 'Expected seven equipment records'
    $available = Request GET '/api/v1/equipment?available=true' 200
    Assert-That ((($available.Value.equipmentCode | Sort-Object) -join ',') -eq 'EQ-002,EQ-003,EQ-006,EQ-007') 'Available equipment mismatch'
    $null = Request GET '/api/v1/equipment?available=1' 400
    $students = Request GET '/api/v1/students?search=%20aDa%20' 200
    Assert-That ($students.Value.Count -eq 1 -and $students.Value[0].id -eq $s1) 'Partial student search mismatch'
    $null = Request GET '/api/v1/students?search=one&search=two' 400
    Assert-That ((Request GET "/api/v1/students/$s1" 200).Value.studentNumber -eq 'ST-100') 'Student not found by its returned ID'
    Assert-That ((Request GET "/api/v1/equipment/$($items['EQ-001'])" 200).Value.isAvailable -eq $false) 'Camera should be unavailable'
    Assert-That ((Request GET "/api/v1/borrowings/$e1Loan" 200).Value.studentName -eq 'Ada Cruz') 'Borrowing details missed related names'
    Assert-That ((Request GET "/api/v1/students/$s1/borrowings" 200).Value.Count -eq 2) 'Student history count differs'
    Assert-That ((Request GET "/api/v1/equipment/$($items['EQ-001'])/borrowings" 200).Value.Count -eq 1) 'Equipment history count differs'
    Assert-That ((Request GET '/api/v1/borrowings?status=Active' 200).Value.Count -eq 3) 'Active list count differs'
    Assert-That ((Request GET "/api/v1/students/$s4/borrowings" 200).Raw -eq '[]') 'An empty history must be []'
    foreach ($route in @('students', 'equipment', 'borrowings')) { $null = Request GET "/api/v1/$route/$missing" 404 }
    $null = Request GET '/api/v1/students/not-an-id' 400
    $null = Request GET '/api/v1/unknown-route' 404
    $wrong = Request DELETE '/api/v1/students' 405
    Assert-That ($wrong.Allow -match 'GET' -and $wrong.Allow -match 'POST') '405 missing allowed methods'
    OpenApi-Check
    $after = Snapshot
    Same-State $before $after 'Independent reads changed state'
    Trace @{ event = 'Independent reads preserved PostgreSQL state'; before = $before; after = $after }
}

Run-Case T02 {
    $created = Request POST '/api/v1/students' 201 @{ studentNumber = 'ST-500'; fullName = 'Eva Test'; contact = $null }
    $id = $created.Value.id
    Assert-That ($created.Location -eq "/api/v1/students/$id" -and $created.Value.isEligible) 'Create response header or eligibility differs'
    $current = Request GET $created.Location 200
    Assert-That ($current.Value.fullName -eq 'Eva Test' -and $null -eq $current.Value.contact -and $current.ETag) 'Created student details differ'
    $edited = Request PUT $created.Location 200 @{ studentNumber = 'ST-500'; fullName = 'Eva Edited'; contact = 'local demo' } @{ 'If-Match' = $current.ETag }
    Assert-That ($edited.Value.contact -eq 'local demo') 'Contact edit did not persist in the response'
    $null = Request PUT $created.Location 409 @{ studentNumber = 'ST-500'; fullName = 'Stale edit'; contact = $null } @{ 'If-Match' = $current.ETag }
    $null = Request POST '/api/v1/students' 400 @{ studentNumber = 'ST-600'; fullName = ' ' }
    $null = Request POST '/api/v1/students' 409 @{ studentNumber = ' st-100 '; fullName = 'Duplicate' }
    $null = Request PUT $created.Location 400 @{ studentNumber = 'ST-500'; fullName = 'Missing optional value on full update' }
    $cleared = Request PUT $created.Location 200 @{ studentNumber = 'ST-500'; fullName = 'Eva Edited'; contact = $null }
    Assert-That ($null -eq $cleared.Value.contact) 'Null did not clear contact'
    $restricted = Request PATCH "/api/v1/students/$s1/eligibility" 200 @{ isEligible = $false }
    Assert-That (!$restricted.Value.isEligible) 'Student restriction was not applied'
    Same-State $restricted.Before.borrowings $restricted.After.borrowings 'Restriction changed borrowing records'
    $null = Request POST '/api/v1/borrowings' 409 @{ studentId = $s1; equipmentId = $items['EQ-002']; expectedReturnDate = $due }
    $null = Request POST "/api/v1/borrowings/$e1Loan/return" 200 @{}
    $null = Request PATCH "/api/v1/students/$s1/eligibility" 200 @{ isEligible = $true }
    $null = Request POST '/api/v1/borrowings' 201 @{ studentId = $s1; equipmentId = $items['EQ-002']; expectedReturnDate = $due }
    $repeat = Request PATCH "/api/v1/students/$s1/eligibility" 200 @{ isEligible = $true }
    Same-State $repeat.Before $repeat.After 'Repeating eligibility changed records'
    Stop-Api
    Start-Api
    Assert-That ((Request GET $created.Location 200).Value.fullName -eq 'Eva Edited') 'Student edit was lost at restart'
    Trace @{ event = 'PostgreSQL after API restart'; state = (Snapshot) }
}

Run-Case T03 {
    $created = Request POST '/api/v1/equipment' 201 @{ equipmentCode = 'EQ-008'; name = 'Light'; description = $null }
    Assert-That ($created.Value.isAvailable -and $created.Location -eq "/api/v1/equipment/$($created.Value.id)") 'Equipment create response or availability differs'
    Assert-That ((Request GET $created.Location 200).Value.name -eq 'Light') 'Created equipment Location did not resolve'
    $null = Request PUT $created.Location 200 @{ equipmentCode = 'EQ-008'; name = 'Edited light'; description = 'LED' }
    $camera = "/api/v1/equipment/$($items['EQ-001'])"
    $edited = Request PUT $camera 200 @{ equipmentCode = 'EQ-001'; name = 'Edited camera'; description = $null }
    Assert-That (!$edited.Value.isAvailable) 'Descriptive edit changed availability'
    Same-State $edited.Before.borrowings $edited.After.borrowings 'Equipment edit changed relationships or history'
    $null = Request PUT $created.Location 400 @{ equipmentCode = 'EQ-008'; name = ''; description = $null }
    $null = Request PUT $created.Location 409 @{ equipmentCode = ' eq-001 '; name = 'Dup'; description = $null }
    $null = Request PUT $camera 400 @{ equipmentCode = 'EQ-001'; name = 'Protected'; description = $null; isAvailable = $true }
    $null = Request POST '/api/v1/equipment' 400 @{ equipmentCode = 'EQ-009'; name = 'Protected'; id = 9 }
    Stop-Api
    Start-Api
    Assert-That ((Request GET $created.Location 200).Value.name -eq 'Edited light') 'Equipment edit was lost at restart'
    Assert-That ((Request GET "$camera/borrowings" 200).Value[0].id -eq $e1Loan) 'Equipment relationship changed at restart'
    Trace @{ event = 'PostgreSQL after API restart'; state = (Snapshot) }
}

Run-Case T04 {
    $body = @{ studentId = $s4; equipmentId = $items['EQ-002']; expectedReturnDate = $due }
    $borrowed = Request POST '/api/v1/borrowings' 201 $body
    $id = $borrowed.Value.id
    Assert-That ($borrowed.Location -eq "/api/v1/borrowings/$id") 'Borrowing Location missing'
    Assert-That ($borrowed.Value.borrowedDate -ceq '2026-11-10' -and $borrowed.Value.returnedDate -eq $null -and $borrowed.Value.status -ceq 'Active') 'Checkout date/state differs'
    Assert-That ((Request GET $borrowed.Location 200).Value.id -eq $id) 'New borrowing did not resolve'
    $null = Request POST '/api/v1/borrowings' 409 @{ studentId = $s2; equipmentId = $items['EQ-006']; expectedReturnDate = $due }
    $null = Request POST '/api/v1/borrowings' 409 @{ studentId = $s4; equipmentId = $items['EQ-001']; expectedReturnDate = $due }
    $null = Request POST '/api/v1/borrowings' 404 @{ studentId = $missing; equipmentId = $items['EQ-006']; expectedReturnDate = $due }
    $null = Request POST '/api/v1/borrowings' 400 @{ studentId = $s4; equipmentId = $items['EQ-006']; expectedReturnDate = '2026-11-10' }
    $null = Request POST '/api/v1/borrowings' 400 @{ studentId = $s4; equipmentId = $items['EQ-006']; expectedReturnDate = '2026-11-12T00:00:00' }
    $null = Request POST '/api/v1/borrowings' 400 @{ studentId = "$s4"; equipmentId = $items['EQ-006']; expectedReturnDate = $due }
    $null = Request POST "$($borrowed.Location)/return" 400 @{ returnedDate = $due }
    $returned = Request POST "$($borrowed.Location)/return" 200 @{}
    Assert-That ($returned.Value.status -ceq 'Returned' -and $returned.Value.id -eq $id -and $returned.Value.returnedDate -ceq '2026-11-10') 'Return must retain the same record and use the server date'
    Assert-That (($returned.After.equipment | Where-Object Identifier -eq 'EQ-002').IsAvailable) 'Return did not restore equipment availability'
    $null = Request POST "$($borrowed.Location)/return" 409 @{}
    $null = Request POST "/api/v1/borrowings/$missing/return" 404 @{}
    Stop-Api
    Restart-Postgres
    Start-Api
    Assert-That ((Request GET $borrowed.Location 200).Value.status -ceq 'Returned') 'Return was lost at restart'
    Same-State $returned.After (Snapshot) 'PostgreSQL data changed at restart'
    Trace @{ event = 'PostgreSQL after API and database restart'; state = (Snapshot) }
}

. (Join-Path $PSScriptRoot 'lab5-cases-continuation.ps1')
