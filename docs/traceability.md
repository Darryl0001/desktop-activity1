# Use-case and acceptance traceability

Test names refer to tests/EquipmentBorrowing.Tests. Actual run results and UI/restart limits are in [verification.md](verification.md). Test coverage is acceptance evidence, not a claim that every path was manually clicked.

| Case / acceptance | Implemented staff interaction | Application / repository operation | Verification evidence |
|---|---|---|---|
| UC-01 / A01 | Catalog tab; Find equipment; identifiers, names, description, availability | EquipmentAsync(false) | CatalogAvailableSingleBorrowReturnAndFreshContextReadback; headless tab renders |
| UC-02 / A02 | Available only + reload; optional text search | EquipmentAsync(true), excludes active loans | CatalogAvailableSingleBorrowReturnAndFreshContextReadback |
| UC-03 / A03 | Student/date + selected row + Borrow selected row | LabService.CheckoutAsync, Multiple=false | CatalogAvailableSingleBorrowReturnAndFreshContextReadback; invalid/missing/unavailable/limit/eligibility cases; process restart probe |
| UC-04 / A04 | Borrowings tab → Active only | LoansAsync, Active filter | CatalogAvailableSingleBorrowReturnAndFreshContextReadback; SearchEveryFieldStatusSortCombinedResetAndSeparateHistories |
| UC-05 / A05 | Select borrowing → Confirm return | ReturnAsync, reload after locks, same-record update | CatalogAvailableSingleBorrowReturnAndFreshContextReadback; RestrictionBlocksNewLoansButAllowsReturnAndPermit; restart probe |
| UC-06 / A06 | Check two or three item boxes + student/date → checkout | Shared atomic CheckoutAsync | MultiItemCommitAndActiveLimit; InvalidMultiSelectionsDatesAndMissingReferencesLeaveNoChanges; UnavailableSelectionRejectsEntireCheckout; FailureAfterSqlWritesRollsBackAllBorrowingsAndAvailability; concurrent tests |
| UC-07 / A07 | Find/select student → Load selected student history | LoansAsync(StudentId) | SearchEveryFieldStatusSortCombinedResetAndSeparateHistories; AsyncHistoryShowsBusyStateAndDiscardsResultsAfterSelectionChanges; stale/missing tests |
| UC-08 / A08 | Find/select equipment → Load selected equipment history | LoansAsync(EquipmentId) | EquipmentRegistrationEditsPreserveAvailabilityAndHistory; SearchEveryFieldStatusSortCombinedResetAndSeparateHistories; stale/missing tests |
| UC-09 / A09 | Search + status + sort/direction; Apply; Clear criteria | LoansAsync combined server query | SearchEveryFieldStatusSortCombinedResetAndSeparateHistories; connection-failure test |
| UC-12 / A12 | Students → New student → Save | SaveStudentAsync(Id=0) | StudentRegistrationEditUniquenessDefaultsAndHistory; database constraints |
| UC-13 / A13 | Locate/select student → edit details → Save | SaveStudentAsync(Id, xmin) | StudentRegistrationEditUniquenessDefaultsAndHistory; StaleEditsAndMissingSelectionsAreRejected |
| UC-14 / A14 | Locate/select student → Permit / Restrict | EligibilityAsync, row lock/version check | RestrictionBlocksNewLoansButAllowsReturnAndPermit; stale tests |
| UC-15 / A15 | Catalog → New equipment → Save | SaveEquipmentAsync(Id=0) | EquipmentRegistrationEditsPreserveAvailabilityAndHistory; database constraints |
| UC-16 / A16 | Locate/select item → edit descriptive fields → Save | SaveEquipmentAsync(Id, xmin); no transaction-field inputs | EquipmentRegistrationEditsPreserveAvailabilityAndHistory; stale tests |
| Cross-cutting / A17 | Explicit migrate and seed commands against clean database | Npgsql, generated migrations, DatabaseSetup | CleanMigrationAndSafeSeed; DatabaseRejectsNormalizedDuplicatesBlankFieldsAndBrokenForeignKeys; DatabaseRejectsDuplicateActiveItemAndInvalidDatesAndProtectsHistory |
| Cross-cutting / A18 | Failure messages, reconnect, explicit review after unknown | WriteAsync transaction lifecycle; per-operation factory | FailureAfterSqlWritesRollsBackAllBorrowingsAndAvailability; LostCommitAcknowledgementIsUnknownAndReadbackFindsCompleteCommit; UnknownOutcomeBlocksWritesUntilSuccessfulRefreshAndStaffReview; ConnectionFailureIsSanitizedAndRecoveryWorks |
| Cross-cutting / A19 | Follow setup, inspect diagrams/suite/history/evidence | README and docs; preserved Git history | verification.md, git-history.txt, supplied-specification.txt, schema-and-decisions.md |
