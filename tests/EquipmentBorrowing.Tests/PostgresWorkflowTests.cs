using System.Data.Common;
using EquipmentBorrowing.Application;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace EquipmentBorrowing.Tests;

// An absent test connection is reported as SKIPPED, never a false persistence pass.
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute([System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0) : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LAB4_TEST_CONNECTION_STRING")))
            Skip = "Set LAB4_TEST_CONNECTION_STRING to a disposable PostgreSQL server with CREATEDB permission.";
    }
}
public sealed class PostgresWorkflowTests : IAsyncLifetime
{
    private string connection = "";
    private string database = "";
    private string admin = "";
    private TestFactory factory = null!;
    private LabService service = null!;
    internal LabService AppService => service;
    private static readonly DateTime Today = new(2026, 11, 20);
    public async ValueTask InitializeAsync()
    {
        admin = Environment.GetEnvironmentVariable("LAB4_TEST_CONNECTION_STRING") ?? "";
        if (admin.Length == 0) return;
        database = "lab4_verify_" + Guid.NewGuid().ToString("N");
        await using var conn = new NpgsqlConnection(admin);
        await conn.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", conn);
        await command.ExecuteNonQueryAsync();
        connection = new NpgsqlConnectionStringBuilder(admin) { Database = database }.ConnectionString;
        factory = new(connection);
        service = Service(factory);
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        await DatabaseSetup.SeedAsync(db);
    }
    public async ValueTask DisposeAsync()
    {
        if (database.Length == 0) return;
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(admin);
        await conn.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", conn);
        await command.ExecuteNonQueryAsync();
    }
    private static LabService Service(TestFactory f) => new(new PostgresLabRepository(f), new FixedCalendar());
    private sealed class FixedCalendar : ICalendar { public DateTime Today => PostgresWorkflowTests.Today; }
    private sealed class TestFactory(string cs, params IInterceptor[] interceptors) : IDbContextFactory<EquipmentBorrowingDbContext>
    {
        public EquipmentBorrowingDbContext CreateDbContext() => new(new DbContextOptionsBuilder<EquipmentBorrowingDbContext>()
            .UseNpgsql(cs).AddInterceptors(interceptors).Options);
    }
    private async Task<(Student Student, Equipment[] Items)> Selection()
    {
        var student = (await service.StudentsAsync()).First(s => s.StudentNumber == "2026-0001");
        return (student, (await service.EquipmentAsync()).OrderBy(e => e.Id).ToArray());
    }
    private Task<WriteResult> Checkout(int student, bool multi, params int[] ids) =>
        service.CheckoutAsync(new(student, ids, Today.AddDays(7), multi));
    private async Task<LoanDetails[]> History(int student) => (await service.LoansAsync(new(StudentId: student))).ToArray();

    [PostgresFact] public async Task CleanMigrationAndSafeSeed()
    {
        await using var db = factory.CreateDbContext();
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        await DatabaseSetup.SeedAsync(db);
        Assert.Equal(3, await db.Students.CountAsync());
        Assert.Equal(6, await db.Equipment.CountAsync());
        Assert.Empty(await service.LoansAsync(new()));
    }
    [PostgresFact] public async Task CatalogAvailableSingleBorrowReturnAndFreshContextReadback()
    {
        var (student, items) = await Selection();
        Assert.Equal(6, (await service.EquipmentAsync(true)).Count);
        Assert.True((await Checkout(student.Id, false, items[0].Id)).IsSuccess);
        var fresh = Service(new(connection)); // new service, factory, context, and connection
        var loan = Assert.Single(await fresh.LoansAsync(new(Filter: LoanFilter.Active)));
        Assert.Equal(student.Name, loan.StudentName);
        Assert.Equal(items[0].Name, loan.EquipmentName);
        Assert.Equal(5, (await fresh.EquipmentAsync(true)).Count);
        Assert.True((await fresh.ReturnAsync(loan.Id)).IsSuccess); // early return permitted
        Assert.False((await fresh.ReturnAsync(loan.Id)).IsSuccess);
        var returned = Assert.Single(await Service(new(connection)).LoansAsync(new(Filter: LoanFilter.Returned)));
        Assert.Equal(Today, returned.ActualReturnDate);
        Assert.Empty(await fresh.LoansAsync(new(Filter: LoanFilter.Active)));
        Assert.Equal(6, (await fresh.EquipmentAsync(true)).Count);
    }
    [PostgresFact] public async Task MultiItemCommitAndActiveLimit()
    {
        var (s, e) = await Selection();
        Assert.True((await Checkout(s.Id, true, e[0].Id, e[1].Id)).IsSuccess);
        Assert.Equal(2, (await History(s.Id)).Length);
        Assert.False((await Checkout(s.Id, true, e[2].Id, e[3].Id)).IsSuccess);
        Assert.True((await Checkout(s.Id, false, e[2].Id)).IsSuccess);
        Assert.False((await Checkout(s.Id, false, e[3].Id)).IsSuccess);
        Assert.Equal(3, (await History(s.Id)).Length);
    }
    [PostgresFact] public async Task InvalidMultiSelectionsDatesAndMissingReferencesLeaveNoChanges()
    {
        var (s, e) = await Selection();
        var requests = new[] {
            new CheckoutRequest(s.Id, Array.Empty<int>(), Today.AddDays(1), true),
            new CheckoutRequest(s.Id, new[] { e[0].Id }, Today.AddDays(1), true),
            new CheckoutRequest(s.Id, new[] { e[0].Id, e[0].Id }, Today.AddDays(1), true),
            new CheckoutRequest(s.Id, new[] { e[0].Id, e[1].Id }, null, true),
            new CheckoutRequest(s.Id, new[] { e[0].Id, e[1].Id }, Today, true),
            new CheckoutRequest(s.Id, new[] { e[0].Id, e[1].Id }, Today.AddDays(-1), true),
            new CheckoutRequest(int.MaxValue, new[] { e[0].Id, e[1].Id }, Today.AddDays(1), true),
            new CheckoutRequest(s.Id, new[] { e[0].Id, int.MaxValue }, Today.AddDays(1), true),
            new CheckoutRequest(s.Id, new[] { e[0].Id, -1 }, Today.AddDays(1), true)
        };
        foreach (var request in requests) Assert.False((await service.CheckoutAsync(request)).IsSuccess);
        Assert.Empty(await History(s.Id));
        Assert.Equal(6, (await service.EquipmentAsync(true)).Count);
    }
    [PostgresFact] public async Task UnavailableSelectionRejectsEntireCheckout()
    {
        var (s, e) = await Selection();
        Assert.True((await Checkout(s.Id, false, e[1].Id)).IsSuccess);
        Assert.False((await Checkout(s.Id, true, e[0].Id, e[1].Id)).IsSuccess);
        Assert.Single(await History(s.Id));
        Assert.Contains(await service.EquipmentAsync(true), i => i.Id == e[0].Id);
    }
    [PostgresFact] public async Task StudentRegistrationEditUniquenessDefaultsAndHistory()
    {
        Assert.True((await service.SaveStudentAsync(new(0, "  ST-X  ", "  New Student  ", " "))).IsSuccess);
        var student = Assert.Single(await service.StudentsAsync(), s => s.StudentNumber == "ST-X");
        Assert.True(student.IsAllowedToBorrow); Assert.Null(student.Contact); Assert.Equal("New Student", student.Name);
        Assert.False((await service.SaveStudentAsync(new(0, "st-x", "Duplicate", null))).IsSuccess);
        Assert.False((await service.SaveStudentAsync(new(0, " ", "Name", null))).IsSuccess);
        Assert.False((await service.SaveStudentAsync(new(0, "new", " ", null))).IsSuccess);
        var (_, e) = await Selection();
        Assert.True((await Checkout(student.Id, false, e[0].Id)).IsSuccess);
        Assert.True((await service.SaveStudentAsync(new(student.Id, "ST-Y", "Changed", "contact", student.Version))).IsSuccess);
        var loan = Assert.Single(await History(student.Id));
        Assert.Equal("Changed", loan.StudentName); Assert.Equal("ST-Y", loan.StudentNumber);
        var updated = (await service.StudentsAsync()).Single(s => s.Id == student.Id);
        Assert.True((await service.SaveStudentAsync(new(updated.Id, updated.StudentNumber, updated.Name, " ", updated.Version))).IsSuccess);
        Assert.Null((await Service(new(connection)).StudentsAsync()).Single(s => s.Id == student.Id).Contact);
        Assert.False((await service.SaveStudentAsync(new(updated.Id, "2026-0001", "Conflict", null, updated.Version))).IsSuccess);
    }
    [PostgresFact] public async Task EquipmentRegistrationEditsPreserveAvailabilityAndHistory()
    {
        Assert.True((await service.SaveEquipmentAsync(new(0, "  Kit-X ", " Kit ", " "))).IsSuccess);
        var item = (await service.EquipmentAsync()).Single(e => e.Identifier == "Kit-X");
        Assert.True(item.IsAvailable); Assert.Null(item.Description);
        Assert.False((await service.SaveEquipmentAsync(new(0, "kit-x", "Duplicate", null))).IsSuccess);
        Assert.False((await service.SaveEquipmentAsync(new(0, "", "Missing", null))).IsSuccess);
        Assert.False((await service.SaveEquipmentAsync(new(0, "Kit-Y", " ", null))).IsSuccess);
        var (s, _) = await Selection();
        Assert.True((await Checkout(s.Id, false, item.Id)).IsSuccess);
        item = (await service.EquipmentAsync()).Single(e => e.Id == item.Id);
        Assert.True((await service.SaveEquipmentAsync(new(item.Id, "Kit-Y", "Renamed kit", "type", item.Version))).IsSuccess);
        var fresh = (await Service(new(connection)).EquipmentAsync()).Single(e => e.Id == item.Id);
        Assert.False(fresh.IsAvailable);
        var loan = Assert.Single(await service.LoansAsync(new(EquipmentId: item.Id)));
        Assert.Equal("Renamed kit", loan.EquipmentName); Assert.Equal("Kit-Y", loan.Identifier);
        Assert.True((await service.SaveEquipmentAsync(new(fresh.Id, fresh.Identifier, fresh.Name, "", fresh.Version))).IsSuccess);
        Assert.Null((await service.EquipmentAsync()).Single(e => e.Id == item.Id).Description);
    }
    [PostgresFact] public async Task RestrictionBlocksNewLoansButAllowsReturnAndPermit()
    {
        var (s, e) = await Selection();
        Assert.True((await Checkout(s.Id, false, e[0].Id)).IsSuccess);
        Assert.True((await service.EligibilityAsync(s.Id, s.Version, false)).IsSuccess);
        var restricted = (await service.StudentsAsync()).Single(x => x.Id == s.Id);
        Assert.False(restricted.IsAllowedToBorrow);
        Assert.True((await service.EligibilityAsync(s.Id, restricted.Version, false)).IsSuccess);
        Assert.False((await Checkout(s.Id, false, e[1].Id)).IsSuccess);
        var loan = Assert.Single(await History(s.Id));
        Assert.True((await service.ReturnAsync(loan.Id)).IsSuccess);
        Assert.True((await service.EligibilityAsync(s.Id, restricted.Version, true)).IsSuccess);
        Assert.True((await Checkout(s.Id, false, e[1].Id)).IsSuccess);
        Assert.Equal(2, (await History(s.Id)).Length);
    }
    [PostgresFact] public async Task StaleEditsAndMissingSelectionsAreRejected()
    {
        var (s, e) = await Selection();
        Assert.True((await service.SaveStudentAsync(new(s.Id, s.StudentNumber, "Updated", null, s.Version))).IsSuccess);
        Assert.False((await service.SaveStudentAsync(new(s.Id, s.StudentNumber, "Stale", null, s.Version))).IsSuccess);
        Assert.False((await service.EligibilityAsync(s.Id, s.Version, false)).IsSuccess);
        Assert.True((await service.SaveEquipmentAsync(new(e[0].Id, e[0].Identifier, "Updated", null, e[0].Version))).IsSuccess);
        Assert.False((await service.SaveEquipmentAsync(new(e[0].Id, e[0].Identifier, "Stale", null, e[0].Version))).IsSuccess);
        Assert.False((await service.SaveStudentAsync(new(int.MaxValue, "Missing", "Name", null))).IsSuccess);
        Assert.False((await service.SaveEquipmentAsync(new(int.MaxValue, "Missing", "Name", null))).IsSuccess);
        Assert.False((await service.ReturnAsync(int.MaxValue)).IsSuccess);
        await Assert.ThrowsAsync<RecordMissingException>(() => service.LoansAsync(new(StudentId: int.MaxValue)));
        await Assert.ThrowsAsync<RecordMissingException>(() => service.LoansAsync(new(EquipmentId: int.MaxValue)));
    }
    [PostgresFact] public async Task SearchEveryFieldStatusSortCombinedResetAndSeparateHistories()
    {
        var (s, e) = await Selection();
        var other = (await service.StudentsAsync()).First(x => x.Id != s.Id);
        Assert.True((await Checkout(s.Id, false, e[0].Id)).IsSuccess);
        var first = Assert.Single(await History(s.Id));
        Assert.True((await service.ReturnAsync(first.Id)).IsSuccess);
        Assert.True((await service.CheckoutAsync(new(other.Id, new[] { e[1].Id }, Today.AddDays(2), false))).IsSuccess);
        foreach (var text in new[] { "  juAN ", "2026-0001", e[0].Name.ToUpperInvariant() })
            Assert.Equal(first.Id, Assert.Single(await service.LoansAsync(new(Search: text))).Id);
        Assert.Single(await service.LoansAsync(new(Filter: LoanFilter.Returned)));
        Assert.Single(await service.LoansAsync(new(Filter: LoanFilter.Active)));
        foreach (var sort in Enum.GetValues<LoanSort>())
        foreach (var descending in new[] { false, true })
        {
            var actual = await service.LoansAsync(new(Sort: sort, Descending: descending));
            Func<LoanDetails, DateTime> key = b => sort == LoanSort.Checkout ? b.BorrowedDate : b.ExpectedReturnDate;
            var expected = (descending ? actual.OrderByDescending(key) : actual.OrderBy(key)).ThenBy(b => b.Id);
            Assert.Equal(expected.Select(b => b.Id), actual.Select(b => b.Id));
        }
        Assert.Empty(await service.LoansAsync(new(Search: "Juan", Filter: LoanFilter.Active)));
        Assert.Empty(await service.LoansAsync(new(Search: "no matches")));
        Assert.Equal(2, (await service.LoansAsync(new())).Count);
        Assert.All(await History(s.Id), b => Assert.Equal(s.Id, b.StudentId));
        Assert.All(await service.LoansAsync(new(EquipmentId: e[1].Id)), b => Assert.Equal(other.Id, b.StudentId));
        Assert.Empty(await service.LoansAsync(new(EquipmentId: e[2].Id)));
        Assert.Empty(await service.LoansAsync(new(Search: "%"))); // literal wildcard character
    }
    [PostgresFact] public async Task ConcurrentSameItemCheckoutsProduceOneWinner()
    {
        var (s, e) = await Selection();
        var other = (await service.StudentsAsync()).First(x => x.Id != s.Id);
        var results = await Task.WhenAll(Checkout(s.Id, false, e[0].Id), Checkout(other.Id, false, e[0].Id));
        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(await service.LoansAsync(new()));
    }
    [PostgresFact] public async Task ConcurrentRequestsCannotExceedStudentLimit()
    {
        var (s, e) = await Selection();
        Assert.True((await Checkout(s.Id, true, e[0].Id, e[1].Id)).IsSuccess);
        var results = await Task.WhenAll(Checkout(s.Id, false, e[2].Id), Checkout(s.Id, false, e[3].Id));
        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal(3, (await History(s.Id)).Length);
    }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default) =>
            throw new DbUpdateException("Controlled failure after SQL writes, before commit.");
    }
    [PostgresFact] public async Task FailureAfterSqlWritesRollsBackAllBorrowingsAndAvailability()
    {
        var (s, e) = await Selection();
        var broken = Service(new(connection, new FailAfterSave()));
        var result = await broken.CheckoutAsync(new(s.Id, new[] { e[0].Id, e[1].Id }, Today.AddDays(7), true));
        Assert.False(result.IsSuccess); Assert.False(result.OutcomeUnknown);
        Assert.Empty(await History(s.Id));
        Assert.Equal(6, (await service.EquipmentAsync(true)).Count);
        Assert.True((await Checkout(s.Id, true, e[0].Id, e[1].Id)).IsSuccess);
    }
    private sealed class LoseCommitAcknowledgement : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken ct = default) =>
            throw new IOException("Simulated loss of commit acknowledgement.");
    }
    [PostgresFact] public async Task LostCommitAcknowledgementIsUnknownAndReadbackFindsCompleteCommit()
    {
        var (s, e) = await Selection();
        var uncertain = Service(new(connection, new LoseCommitAcknowledgement()));
        var result = await uncertain.CheckoutAsync(new(s.Id, new[] { e[0].Id, e[1].Id }, Today.AddDays(7), true));
        Assert.False(result.IsSuccess); Assert.True(result.OutcomeUnknown);
        Assert.Equal(2, (await History(s.Id)).Length);
        Assert.Equal(4, (await service.EquipmentAsync(true)).Count);
    }
    [PostgresFact] public async Task ConnectionFailureIsSanitizedAndRecoveryWorks()
    {
        var bad = Service(new("Host=127.0.0.1;Port=1;Database=missing;Username=test;Password=SECRET_SENTINEL;Timeout=1"));
        var result = await bad.SaveStudentAsync(new(0, "X", "Name", null));
        Assert.False(result.IsSuccess); Assert.False(result.OutcomeUnknown);
        Assert.DoesNotContain("SECRET_SENTINEL", result.Message);
        Assert.Equal(3, (await service.StudentsAsync()).Count);
    }
    private async Task Constraint(Action<EquipmentBorrowingDbContext> change, string state)
    {
        await using var db = factory.CreateDbContext();
        change(db);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(state, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [PostgresFact] public async Task DatabaseRejectsNormalizedDuplicatesBlankFieldsAndBrokenForeignKeys()
    {
        var (s, e) = await Selection();
        await Constraint(db => db.Students.Add(new Student { Name = "Duplicate", StudentNumber = "  2026-0001 " }), PostgresErrorCodes.UniqueViolation);
        await Constraint(db => db.Equipment.Add(new Equipment { Name = "Duplicate", Identifier = " eq-001 " }), PostgresErrorCodes.UniqueViolation);
        await Constraint(db => db.Equipment.Add(new Equipment { Name = "Duplicate", Identifier = "\tEQ-001\u00a0" }), PostgresErrorCodes.UniqueViolation);
        await Constraint(db => db.Students.Add(new Student { Name = "\t\u00a0", StudentNumber = "blank-unicode" }), PostgresErrorCodes.CheckViolation);
        await Constraint(db => db.Students.Add(new Student { Name = " ", StudentNumber = "blank" }), PostgresErrorCodes.CheckViolation);
        await Constraint(db => db.Equipment.Add(new Equipment { Name = "X", Identifier = " " }), PostgresErrorCodes.CheckViolation);
        await Constraint(db => db.Borrowings.Add(new Borrowing { StudentId = int.MaxValue, EquipmentId = e[0].Id, BorrowedDate = Today, ExpectedReturnDate = Today.AddDays(1) }), PostgresErrorCodes.ForeignKeyViolation);
    }
    [PostgresFact] public async Task DatabaseRejectsDuplicateActiveItemAndInvalidDatesAndProtectsHistory()
    {
        var (s, e) = await Selection();
        Assert.True((await Checkout(s.Id, false, e[0].Id)).IsSuccess);
        await Constraint(db => db.Borrowings.Add(new Borrowing { StudentId = s.Id, EquipmentId = e[0].Id, BorrowedDate = Today, ExpectedReturnDate = Today.AddDays(1) }), PostgresErrorCodes.UniqueViolation);
        await Constraint(db => db.Borrowings.Add(new Borrowing { StudentId = s.Id, EquipmentId = e[1].Id, BorrowedDate = Today, ExpectedReturnDate = Today }), PostgresErrorCodes.CheckViolation);
        await Constraint(db => db.Borrowings.Add(new Borrowing { StudentId = s.Id, EquipmentId = e[1].Id, BorrowedDate = Today, ExpectedReturnDate = Today.AddDays(1), Status = BorrowingStatus.Returned }), PostgresErrorCodes.CheckViolation);
        await Constraint(db => db.Students.Remove(s), PostgresErrorCodes.RestrictViolation);
        Assert.Single(await History(s.Id));
    }
}
