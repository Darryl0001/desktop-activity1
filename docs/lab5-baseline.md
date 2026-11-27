# Lab 5 implementation baseline

| Field | Record |
| --- | --- |
| Lab 4 baseline | `bcb791c45c8950226f8f44ea0edb43a7d6a2dc3a` (`laboratory 4 commit`) in the original `Darryl0001/desktop-activity1` repository. The checked-out `main` and local `origin/main` both pointed to this revision when Lab 5 began. |
| Baseline limitation | The commit and local remote-tracking reference identify the Lab 4 snapshot. A classroom submission receipt was unavailable for independent comparison. |
| Lab 5 revision | Verified implementation commit `ae5885fb15261daccca755ed72dd759826033940`. The `lab5-submitted` tag also includes the consolidated results and evidence. Run `git rev-parse lab5-submitted^{commit}` to resolve the full submission hash. |
| Persistence migration | The two original PostgreSQL migrations and their model snapshot are carried forward. Lab 5 adds no schema change. The verification runner applies both migrations to a fresh database. |
| Setup and evidence | See the [README](../README.md) and [consolidated acceptance record](lab5-acceptance.md). |

## Architecture

```text
Lab 4  Avalonia → WorkbenchViewModel → LabService → ILabRepository
           → PostgresLabRepository → EF Core → PostgreSQL

Lab 5  Avalonia → WorkbenchViewModel → HttpLabClient → HTTP /api/v1
           → ASP.NET Core endpoints → LabService → ILabRepository
           → PostgresLabRepository → EF Core → PostgreSQL
```

The desktop composition root in [App.axaml.cs](../EquipmentBorrowing.Desktop/App.axaml.cs) registers the HTTP client. The [desktop project](../EquipmentBorrowing.Desktop/EquipmentBorrowing.Desktop.csproj) references shared application and contract assemblies. Only the API and setup executable configure PostgreSQL for normal application use. The API registers a scoped `LabService`, scoped repository, and a context factory in [Program.cs](../src/EquipmentBorrowing.Api/Program.cs). Each repository operation creates and disposes its own context. The migration and demo seed commands remain in the setup executable.

The [repository](../src/EquipmentBorrowing.Infrastructure/PostgresLabRepository.cs) retains Lab 4 transaction ownership, lock ordering, the availability and active limit checks, and PostgreSQL constraints. Resource responses use snapshots assembled before commit and returned after successful commit. Those snapshots carry the generated resource IDs without a second database read after a successful write. The API uses typed write outcomes to select HTTP status codes. Public resource tags support optimistic edit checks, with the original PostgreSQL row versions protecting concurrent updates inside the server.

The [Avalonia ViewModel](../EquipmentBorrowing.Desktop/ViewModels/WorkbenchViewModel.cs) awaits HTTP operations, preserves async busy state and selection, and refreshes records and any loaded histories after mutations. The [client](../EquipmentBorrowing.Desktop/Services/HttpLabClient.cs) distinguishes an unavailable transport from an HTTP error response. When a write outcome is unknown, the existing review workflow requires a refresh and acknowledgement before another write. T07 records the desktop's resolved dependencies and exercises the actual HTTP transport from the bound Avalonia window.
