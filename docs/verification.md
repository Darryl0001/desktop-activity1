# Lab 4 verification evidence

Run date: **20 November 2026, Asia/Manila**. Environment: Windows x64, .NET SDK 10.0.400, PostgreSQL 18.6, EF Core 10.0.12, Npgsql EF provider 10.0.2, Avalonia 12.1.1.

The reproducible command is `./scripts/verify-postgres.ps1`. It creates its own loopback-only PostgreSQL cluster and disposable databases. The user's existing PostgreSQL service/databases are not used. [Machine-readable test results](evidence/lab4-postgres.trx) and [restart readback](evidence/restart-readback.txt) accompany this document.

## Automated expected versus observed results

The final suite contains **21 tests: 21 passed, 0 failed, 0 skipped** with a configured PostgreSQL test connection. Build: **0 warnings, 0 errors**. Each database test applies the actual migrations to a clean database; this is real PostgreSQL evidence, not an in-memory provider or build-only claim.

| Acceptance | Scenario and expected result | Observed result |
|---|---|---|
| A01–A03 | Catalog initially has six available seeded items. Single checkout persists one related active loan and removes its item from available results. | Passed: correct related student/item names and five remaining available items. |
| A03, A06 | Reject missing references, absent/current/past due date, empty/one-item multi request, duplicate/invalid selections, and unavailable items without partial change. | Passed: invalid cases leave no new records; available flags remain consistent. |
| A03, A06 | Two-item checkout commits both loans; active count plus request cannot exceed three. | Passed: two-item issue followed by one succeeds; fourth item and over-limit batch fail. |
| A04–A05 | Active list excludes returned records; early return sets actual date, restores availability, and retains the original loan; repeat return rejected. | Passed, including fresh factory/context readback and separate-process restart evidence. |
| A06 | Two simultaneous students request the same item; only one wins. | Passed: exactly one successful checkout and one active borrowing. |
| A06 | One student with two active items makes simultaneous additional requests. | Passed: one succeeds and one is rejected; final active count is three. |
| A06, A18 | Inject a failure after SQL writes but before commit; batch must roll back all borrowing and availability changes. | Passed: zero loans, all six items available, and a later normal checkout succeeds. Test-only EF SaveChanges interceptor. |
| A07–A09 | Keep student/item histories separate; show active and returned records, empty histories, all search fields, statuses, both date sorts/directions, tie-breaks, combined criteria, clear, and literal wildcard text. | Passed against distinguishable database records. Clearing criteria uses a default query and returns the complete list. |
| A07, A19 | Async history exposes busy state while awaiting I/O and discards results if selection changed. | Passed using a controlled pending task; no records from the previous selection appear. |
| A12–A13 | Register trimmed student number/name with default eligibility and optional contact; reject duplicates/blanks; edit stable identity and clear contact without losing history. | Passed, including fresh context readback of updated details and original linked loans. |
| A14 | Restrict eligibility, block new issue, preserve existing loans, permit return, treat repeated choice safely, then permit valid borrowing again. | Passed; stale eligibility requests are also rejected. |
| A15–A16 | Register trimmed equipment with available state and no loan; reject duplicate/blank identity/name; edit descriptive fields and clear description without changing availability or history. | Passed, including editing an actively borrowed item and fresh context readback. Transaction fields have no descriptive form inputs. |
| A13, A16 | Reject stale xmin versions and missing edited/history selections. | Passed; updated data remains intact. |
| A17 | Clean schema, migration snapshot consistency, and repeatable non-destructive initial seed. | Passed: clean migrations, no pending model changes, three students/six equipment, repeated seed leaves data intact. |
| A17 | Bypass service validation and attempt normalized duplicates, Unicode whitespace blanks, broken FKs, duplicate active equipment, invalid due/return states, and deletion of referenced student. | Passed: PostgreSQL rejected each with the expected constraint code; existing history remained. |
| A18 | Connection failure returns sanitized error and no success; normal repository works after recovery. | Passed: no secret sentinel appeared in feedback, and persisted master data remained readable. |
| A18 | Simulate lost acknowledgement after an actual commit. Do not claim success/rollback or automatically replay. | Passed: result is UNKNOWN; fresh read finds both loans and both unavailable items. This simulates acknowledgement loss with an EF transaction interceptor, not a physical network outage. |
| A18 | Unknown result blocks writes until successful reconnection/refresh and explicit staff review. | Passed: no extra repository write occurs; failed refresh cannot clear the block; successful refresh still requires staff acknowledgement. |
| A19 | Render all three tabs at 1180×820 and 900×650. | Passed; six PNGs generated and visually inspected. Small-window catalog editing/history uses a scrollable pane. |
| A03, A05–A07, A09, A12 | Exercise actual XAML bindings and bound commands with real PostgreSQL. | Passed: TextBox bindings registered a student; the bound batch button issued two items; history/search showed both; the bound return button updated one original record and its actual date. |

Test implementations: [PostgresWorkflowTests.cs](../tests/EquipmentBorrowing.Tests/PostgresWorkflowTests.cs) and [WorkspaceTests.cs](../tests/EquipmentBorrowing.Tests/WorkspaceTests.cs). The initial empty placeholder test was removed.

## Restart persistence

The verification script invokes the test-only PersistenceProbe in separate OS processes. It commits a newly registered student, two newly registered items and two active loans, restarts PostgreSQL, reads both active records in a new process, returns both original loans in another process, restarts PostgreSQL again, and reads the retained returned records, actual dates, and available items in a final process. All phases must exit successfully; the script stops the disposable server in finally.

This verifies process/server restart persistence of the production application service/repository path. It does not claim that a native desktop window was manually closed and reopened.

## UI evidence and remaining manual demonstration

[Database-backed checkout/search/return screenshot](evidence/ui/database-workflow.png) shows one returned and one active record after the bound desktop-command test. [Catalog](evidence/ui/workspace-1180-0.png), [students](evidence/ui/workspace-1180-1.png), and [borrowings](evidence/ui/workspace-900-2.png) include rendering/empty-state evidence. The six generic layout screenshots use clearly identified test data; database-workflow.png uses real disposable PostgreSQL records.

Native Windows UI automation was attempted, but its runtime could not start: `CreateProcessWithLogonW failed: 1920`. The fallback uses Avalonia's actual XAML, bindings, control tree, and Skia rendering through its official [headless test platform](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform). This is not evidence of manual mouse/keyboard operation in a native Windows window. For the instructor walkthrough, launch the desktop with the README steps and demonstrate selection, scrolling, keyboard interaction, and closing/reopening the window.

## Submission checks

The [traceability matrix](traceability.md) maps every required case and A17–A19. The [suite](use-case-suite.md) retains all eighteen supplied fields for fourteen cases. The [catalog](use-cases.md) classifies all thirty-three cases, and the UML source has only actor-to-goal associations. [Schema decisions](schema-and-decisions.md), [idempotent PostgreSQL SQL](postgresql-migrations.sql), earlier documentation, SQLite migration archive, and [Git history export](git-history.txt) are included.

Existing Git history is preserved. Lab 4 source/document changes are working-tree changes and have not been committed by the assistant. Review them and create meaningful development commits before final submission. Student IDs were not supplied; both partners must add their full IDs to their individual submission/private comment.
