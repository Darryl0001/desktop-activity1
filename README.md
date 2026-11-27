# Campus Equipment Borrowing System · Lab 5

The Avalonia desktop app manages students, equipment, checkout, returns, and borrowing history. It uses the ASP.NET Core API for application data. The API can run by itself and stores data in PostgreSQL through EF Core.

## Run locally

Install .NET 10 SDK (10.0.400 or a later compatible patch) and PostgreSQL. For the commands below, make PostgreSQL's `createdb` available on PATH or create the database with pgAdmin.

```powershell
createdb -h localhost -U postgres equipment_borrowing
dotnet restore EquipmentBorrowing.slnx
$env:LAB4_CONNECTION_STRING = 'Host=localhost;Database=equipment_borrowing;Username=postgres;Timeout=5;Command Timeout=15'
dotnet run --project src/EquipmentBorrowing.ConsoleApp -- migrate
dotnet run --project src/EquipmentBorrowing.ConsoleApp -- seed
dotnet run --project src/EquipmentBorrowing.Api
```

Use a local role that owns the database. Supply its password with PostgreSQL's private password file (Windows `%APPDATA%\postgresql\pgpass.conf`) or a private environment variable. The API reads `ConnectionStrings__EquipmentBorrowing` first, then `LAB4_CONNECTION_STRING`. The setup executable uses `LAB4_CONNECTION_STRING`. The `seed` command is optional and initializes the Lab 4 demo data only when both master tables are empty. The same migration command can upgrade an existing Lab 4 PostgreSQL database.

Start the desktop client in its own terminal.

```powershell
$env:LAB5_API_BASE_URL = 'http://127.0.0.1:5185'
dotnet run --project EquipmentBorrowing.Desktop
```

Both processes default to `http://127.0.0.1:5185`. Set the API listening address with `-- --urls http://127.0.0.1:5200` after the API project argument, and set the desktop base URL to match. Keep both on loopback for the lab. The desktop can be restarted or refreshed independently.

Use these commands to verify the API with the desktop closed.

```powershell
Invoke-RestMethod 'http://127.0.0.1:5185/api/v1/equipment?available=true'
Invoke-RestMethod 'http://127.0.0.1:5185/openapi/v1.json'
```

Use the [endpoint catalog](docs/lab5-api.md) and [OpenAPI source](src/EquipmentBorrowing.Api/openapi.json) for request examples and response schemas. Dates use the Asia/Manila calendar.

## Desktop walkthrough

1. Open **Students & history**. Register a student, edit permitted fields, or use the separate eligibility actions. Select a student to load their borrowing history.
2. Open **Catalog & checkout**. Find an item, register or edit equipment, or load its history. Select a student and a future due date. Use **Borrow selected row** for one item or check two or three items and use **Check out checked items (2–3)**.
3. Open **Borrowings & returns**. Combine search, status, and date sort criteria. Select an active row and confirm its return. Check the refreshed availability and history.
4. **Reconnect / refresh** fetches current records. If a write outcome is unknown, refresh and review the affected records or history, then select **Records reviewed** before deciding whether to retry.

## Reproduce acceptance evidence

The Windows verifier requires PowerShell 7.5+ and the PostgreSQL binaries.

```powershell
pwsh -File scripts/verify-lab5.ps1
# Supply -PostgresBin if PostgreSQL was installed at a different path.
```

The script creates a private loopback-only PostgreSQL cluster, applies the existing migrations, and resets its `lab5_acceptance` database to the instructor fixture before every test. The API uses a fixed date of 2026-11-10 in the `Testing` environment. The script exercises all eight cases, runs the Lab 4 regression suite against disposable databases, and stops the processes when finished. Outputs are written to `.local/lab5-evidence` by default. The reset and controlled checkout fault scripts target only that disposable database.

- [Architecture and implementation baseline](docs/lab5-baseline.md)
- [Lab 4 use cases](docs/use-case-suite.md) and [business rules](docs/supplied-specification.txt)
- [Lab 5 traceability and consolidated execution record](docs/lab5-acceptance.md)

The Lab 4 repository records Brent Marcus Ocaya and Darryl Macarandan as members. Add the institutional student IDs privately to each individual submission as required by the handout.
