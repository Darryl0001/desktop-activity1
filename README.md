# Campus Equipment Borrowing System — Lab Activity 4

C#/.NET 10, Avalonia 12, CommunityToolkit.Mvvm, EF Core, and PostgreSQL/Npgsql. Continue the existing repository and its Git history. The operational database is PostgreSQL; SQLite is no longer registered.

## Setup

Install .NET SDK 10.0.400 or a later 10.0 patch, and PostgreSQL 18 (a compatible supported PostgreSQL version should also work). No Docker, cloud account, authentication service, or paid infrastructure is needed.

Create a local database and a PostgreSQL role that owns it using pgAdmin or PostgreSQL tools. For example, after adding PostgreSQL's bin folder to PATH:

```powershell
createdb -h localhost -U postgres equipment_borrowing
dotnet restore EquipmentBorrowing.slnx
```

Set your connection for the current PowerShell session. Replace the local role as appropriate. Prefer PostgreSQL's password file (%APPDATA%\postgresql\pgpass.conf on Windows) so no password needs to appear in commands or source:

```powershell
$env:LAB4_CONNECTION_STRING = 'Host=localhost;Database=equipment_borrowing;Username=postgres;Timeout=5;Command Timeout=15'
dotnet run --project src/EquipmentBorrowing.ConsoleApp -- migrate
dotnet run --project src/EquipmentBorrowing.ConsoleApp -- seed
dotnet run --project EquipmentBorrowing.Desktop
```

A password can be supplied through a private environment variable connection string if required by your local server. Never commit credentials, .env files, or local configuration. The default connection is the password-free localhost example above. Connection failures produce safe feedback; the desktop never applies migrations.

The explicit seed command adds three eligible students and six available items only when both master tables are empty. It runs in a transaction with an advisory lock; repeated calls do not reset details, eligibility, loans, or availability. A partly populated database is left untouched. The initial empty borrowing list is intentional.

The setup executable supports only `migrate` and `seed`. Alternatively install EF tooling with `dotnet tool install --global dotnet-ef --version 10.0.12` and run:

```powershell
dotnet ef database update --project src/EquipmentBorrowing.Infrastructure
dotnet ef migrations script --idempotent --project src/EquipmentBorrowing.Infrastructure
```

Use a fresh PostgreSQL database. Historical SQLite row migration and running both database providers are outside Lab 4 scope. Earlier SQLite migrations and README are preserved in [docs/legacy-sqlite](docs/legacy-sqlite) and [docs/lab3-readme.md](docs/lab3-readme.md).

## Using the desktop

- **Catalog & checkout:** find an item by name/identifier, optionally choose Available only, then Find equipment. Select a student and a due date later than today. Borrow selected row issues one item; checkboxes plus Check out checked items issue two or three items atomically. Register/edit equipment in the side panel; load the selected item's full history there.
- **Students & history:** find by name/number, select a student to edit permitted details, or choose New student to register. Permit/restrict are separate actions. A restriction prevents new checkout while allowing existing items to be returned.
- **Borrowings & returns:** search partial student name, student number, or equipment name; combine All/Active/Returned with checkout/expected-return sorting in either direction. Search applies criteria; Active only and Clear criteria are shortcuts. Select a loan and confirm return.
- Loading indicators and status messages distinguish successful operations, rejections, empty results, and connection failures. Reload/retry after recovery.
- After an **unknown commit outcome**, writes are blocked. Reconnect / refresh successfully, inspect borrowings and histories, then explicitly choose Records reviewed before deciding whether to retry. No automatic write retry occurs.

All borrowing dates are Asia/Manila calendar dates, stored as PostgreSQL `date`. The expected date must be strictly later than checkout. Early return is allowed; actual return cannot precede checkout.

## Architecture and integrity

```text
Avalonia View → WorkbenchViewModel → LabService → ILabRepository
    → PostgresLabRepository → EF Core → PostgreSQL
```

The original Student, Equipment, and Borrowing concepts and stable relationships remain. The former separate-save repositories/services were replaced by an operation-oriented repository abstraction: a checkout cannot accidentally save availability without its borrowing records. The old in-memory demo was replaced with a database setup executable.

DI registers a context factory and stateless repository/service. Every repository operation creates and disposes its own DbContext; UI commands never share a tracked context. Read queries use AsNoTracking, database-side joins/filtering/sorting, and asynchronous I/O. The desktop disables conflicting interaction while work is pending; history selection changes clear old records and late results are discarded. No database configuration, SQL, or migrations occur in Views/ViewModels; the desktop composition root configures the provider.

Each write uses an explicit transaction. Checkout locks the student, recalculates the active count from persisted loans (limit three), then locks equipment in ascending ID order and revalidates availability. Return locks in the same order and saves status, actual date, and availability together. Eligibility changes lock the student, so they coordinate with checkout. Descriptive edits use PostgreSQL xmin concurrency tokens and reject stale selections. Database-generated identity keys avoid max-plus-one races.

PostgreSQL enforces PKs, restrictive FKs, nonblank required fields, optional-field consistency, normalized identifier uniqueness, valid borrowing dates/status, and a partial unique index allowing one active borrowing per equipment item. Identifier normalization trims the Unicode whitespace set used by .NET Trim and compares uppercased values in PostgreSQL. Ordinary registration trims displayed values as well. Application validation supplements these constraints. The active count is queried, never stored as a second mutable counter. See [schema and decisions](docs/schema-and-decisions.md).

Known failures roll back. Connection/timeout/cancellation failures during commit are conservatively reported as unknown, never as success or guaranteed rollback; server-declared constraint/conflict failures are known rejections. Provider messages and connection strings are not exposed.

## Verification and submission

```powershell
dotnet build EquipmentBorrowing.slnx
# Use a disposable server/role with CREATEDB permission:
$env:LAB4_TEST_CONNECTION_STRING = 'Host=localhost;Database=postgres;Username=your_test_role'
dotnet test EquipmentBorrowing.slnx --logger 'trx;LogFileName=lab4-postgres.trx'
```

Each PostgreSQL test creates, migrates, seeds, and drops its own uniquely named `lab4_verify_...` database. It never drops the configured base database. Database tests are explicitly skipped when the test connection variable is absent; skipped tests are not persistence evidence. The non-database UI/recovery tests still run.

[The verification script](scripts/verify-postgres.ps1) can initialize a separate loopback-only cluster using an installed PostgreSQL distribution, run the tests, render UI evidence, and verify restart persistence in separate processes. Its local test cluster uses trust authentication only on loopback, and stops at completion. This is an optional verification convenience, not an application prerequisite.

Submission documents:

- [Use-case diagram and complete catalog](docs/use-cases.md)
- [Fourteen adapted use-case tables](docs/use-case-suite.md)
- [BR-01–19 and original supplied specification](docs/supplied-specification.txt)
- [Use-case/acceptance traceability](docs/traceability.md)
- [Schema, migrations, and architectural decisions](docs/schema-and-decisions.md)
- [Expected versus observed verification evidence](docs/verification.md)
- [Earlier Git history export](docs/git-history.txt); current implementation changes can be inspected with `git diff`

Members retained from the earlier README: **Brent Marcus Ocaya** and **Darryl Macarandan**. Add both full institutional student IDs in each member's individual submission/private comment; the repository did not supply those IDs. Each member must understand and demonstrate the work.
