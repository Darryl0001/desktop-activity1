using EquipmentBorrowing.Application;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure;

public sealed class PostgresLabRepository(IDbContextFactory<EquipmentBorrowingDbContext> factory) : ILabRepository
{
    public Task<IReadOnlyList<Student>> StudentsAsync(CancellationToken ct = default) => SearchStudentsAsync("", ct);

    public async Task<IReadOnlyList<Student>> SearchStudentsAsync(string search, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Students.AsNoTracking();
        if (search.Length > 0)
        {
            var pattern = LiteralPattern(search);
            query = query.Where(s => EF.Functions.ILike(s.Name, pattern, "\\") ||
                EF.Functions.ILike(s.StudentNumber, pattern, "\\"));
        }
        return await query.OrderBy(s => s.Name).ThenBy(s => s.Id).ToListAsync(ct);
    }

    public async Task<Student?> StudentAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Equipment?> EquipmentItemAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Equipment.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<Equipment>> EquipmentAsync(bool availableOnly, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Equipment.AsNoTracking();
        if (availableOnly) query = query.Where(e => e.IsAvailable && !db.Borrowings.Any(b => b.EquipmentId == e.Id && b.Status == BorrowingStatus.Active));
        return await query.OrderBy(e => e.Name).ThenBy(e => e.Id).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<LoanDetails>> LoansAsync(LoanQuery criteria, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (criteria.StudentId is int sid && !await db.Students.AnyAsync(s => s.Id == sid, ct))
            throw new RecordMissingException("The selected student no longer exists. Refresh and reselect.");
        if (criteria.EquipmentId is int eid && !await db.Equipment.AnyAsync(e => e.Id == eid, ct))
            throw new RecordMissingException("The selected equipment no longer exists. Refresh and reselect.");
        var query = from b in db.Borrowings.AsNoTracking()
                    join s in db.Students on b.StudentId equals s.Id
                    join e in db.Equipment on b.EquipmentId equals e.Id
                    select new { b.Id, StudentId = s.Id, s.StudentNumber, StudentName = s.Name,
                        EquipmentId = e.Id, e.Identifier, EquipmentName = e.Name,
                        b.BorrowedDate, b.ExpectedReturnDate, b.Status, b.ActualReturnDate };
        if (criteria.BorrowingId is int borrowingId) query = query.Where(b => b.Id == borrowingId);
        if (criteria.StudentId is int studentId) query = query.Where(b => b.StudentId == studentId);
        if (criteria.EquipmentId is int equipmentId) query = query.Where(b => b.EquipmentId == equipmentId);
        if (criteria.Filter != LoanFilter.All)
            query = query.Where(b => b.Status == (criteria.Filter == LoanFilter.Active ? BorrowingStatus.Active : BorrowingStatus.Returned));
        var search = criteria.Search.Trim();
        if (search.Length > 0)
        {
            // Escape LIKE metacharacters: staff text is literal partial text, not a SQL pattern.
            var pattern = LiteralPattern(search);
            query = query.Where(b => EF.Functions.ILike(b.StudentName, pattern, "\\") ||
                EF.Functions.ILike(b.StudentNumber, pattern, "\\") || EF.Functions.ILike(b.EquipmentName, pattern, "\\"));
        }
        var ordered = (criteria.Sort, criteria.Descending) switch
        {
            (LoanSort.ExpectedReturn, true) => query.OrderByDescending(b => b.ExpectedReturnDate),
            (LoanSort.ExpectedReturn, false) => query.OrderBy(b => b.ExpectedReturnDate),
            (_, true) => query.OrderByDescending(b => b.BorrowedDate),
            _ => query.OrderBy(b => b.BorrowedDate)
        };
        return await ordered.ThenBy(b => b.Id).Select(b => new LoanDetails(b.Id, b.StudentId,
            b.StudentNumber, b.StudentName, b.EquipmentId, b.Identifier, b.EquipmentName,
            b.BorrowedDate, b.ExpectedReturnDate, b.Status, b.ActualReturnDate)).ToListAsync(ct);
    }
    private static string LiteralPattern(string text) =>
        "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    public Task<WriteResult> SaveStudentAsync(StudentEdit edit, CancellationToken ct = default) => WriteAsync(async db =>
    {
        var student = edit.Id == 0 ? new Student() : await db.Students.SingleOrDefaultAsync(s => s.Id == edit.Id, ct);
        if (student is null) return WriteResult.Rejected("Student no longer exists. Refresh and reselect.", WriteFailure.Missing);
        if (edit.Id != 0 && student.Version != edit.Version) return Stale();
        // Compare with exactly the same PostgreSQL expression as the generated unique column.
        if (await db.Students.AnyAsync(s => s.Id != edit.Id && s.StudentNumber.Trim().ToUpper() == edit.Number.Trim().ToUpper(), ct))
            return WriteResult.Rejected("Student number is already registered (case and surrounding spaces are ignored).");
        student.StudentNumber = edit.Number; student.Name = edit.Name; student.Contact = edit.Contact;
        if (edit.Id == 0) db.Students.Add(student);
        await db.SaveChangesAsync(ct);
        return WriteResult.Saved("Student saved.", student.Id) with { Student = student };
    }, ct);
    public Task<WriteResult> SaveEquipmentAsync(EquipmentEdit edit, CancellationToken ct = default) => WriteAsync(async db =>
    {
        var item = edit.Id == 0 ? new Equipment() : await db.Equipment.SingleOrDefaultAsync(e => e.Id == edit.Id, ct);
        if (item is null) return WriteResult.Rejected("Equipment no longer exists. Refresh and reselect.", WriteFailure.Missing);
        if (edit.Id != 0 && item.Version != edit.Version) return Stale();
        if (await db.Equipment.AnyAsync(e => e.Id != edit.Id && e.Identifier.Trim().ToUpper() == edit.Identifier.Trim().ToUpper(), ct))
            return WriteResult.Rejected("Equipment identifier is already registered (case and surrounding spaces are ignored).");
        item.Identifier = edit.Identifier; item.Name = edit.Name; item.Description = edit.Description;
        if (edit.Id == 0) db.Equipment.Add(item);
        await db.SaveChangesAsync(ct);
        return WriteResult.Saved("Equipment saved.", item.Id) with { Equipment = item };
    }, ct);
    public Task<WriteResult> EligibilityAsync(int id, uint version, bool allowed, CancellationToken ct = default) => WriteAsync(async db =>
    {
        var student = await LockStudentAsync(db, id, ct);
        if (student is null) return WriteResult.Rejected("Student no longer exists. Refresh and reselect.", WriteFailure.Missing);
        if (student.Version != version) return Stale();
        if (student.IsAllowedToBorrow == allowed)
            return WriteResult.Saved(allowed ? "Student is already permitted." : "Student is already restricted.", id) with { Student = student };
        student.IsAllowedToBorrow = allowed;
        await db.SaveChangesAsync(ct);
        return WriteResult.Saved(allowed ? "Student permitted to borrow." : "Student restricted. Existing loans and returns are unchanged.", id) with { Student = student };
    }, ct);
    public Task<WriteResult> CheckoutAsync(CheckoutRequest request, DateTime today, CancellationToken ct = default) => WriteAsync(async db =>
    {
        // All checkout callers, including single-item checkout, use the same transaction boundary.
        var ids = request.EquipmentIds;
        if (request.DueDate is null || request.DueDate.Value.Date <= today ||
            ids.Any(id => id <= 0) || ids.Count != ids.Distinct().Count() ||
            (request.Multiple ? ids.Count < 2 : ids.Count != 1))
            return WriteResult.Rejected("Invalid date or item selection. Refresh and correct the request.", WriteFailure.Invalid);
        var student = await LockStudentAsync(db, request.StudentId, ct);
        if (student is null) return WriteResult.Rejected("Student no longer exists. Refresh and reselect.", WriteFailure.Missing);
        if (!student.IsAllowedToBorrow) return WriteResult.Rejected("This student is restricted from new borrowing.");
        var active = await db.Borrowings.CountAsync(b => b.StudentId == student.Id && b.Status == BorrowingStatus.Active, ct);
        if (active + ids.Count > 3) return WriteResult.Rejected($"Three-item limit: {active} active, {ids.Count} requested.");
        var issued = new List<(Borrowing Loan, Equipment Item)>();
        foreach (var id in ids.Order())
        {
            var item = await LockEquipmentAsync(db, id, ct);
            if (item is null) return WriteResult.Rejected("An item no longer exists. Refresh and reselect.", WriteFailure.Missing);
            if (!item.IsAvailable || await db.Borrowings.AnyAsync(b => b.EquipmentId == id && b.Status == BorrowingStatus.Active, ct))
                return WriteResult.Rejected($"Item {item.Identifier} is unavailable. Refresh before another request.");
            item.IsAvailable = false;
            var loan = new Borrowing { StudentId = student.Id, EquipmentId = id,
                BorrowedDate = today.Date, ExpectedReturnDate = request.DueDate.Value.Date, Status = BorrowingStatus.Active };
            db.Borrowings.Add(loan);
            issued.Add((loan, item));
        }
        await db.SaveChangesAsync(ct);
        return WriteResult.Saved($"Checked out {ids.Count} item(s).", issued[0].Loan.Id) with
        {
            Borrowings = issued.Select(entry => Describe(entry.Loan, student, entry.Item)).ToArray()
        };
    }, ct);
    public Task<WriteResult> ReturnAsync(int borrowingId, DateTime today, CancellationToken ct = default) => WriteAsync(async db =>
    {
        var loan = await db.Borrowings.SingleOrDefaultAsync(b => b.Id == borrowingId, ct);
        if (loan is null) return WriteResult.Rejected("Borrowing no longer exists. Refresh and reselect.", WriteFailure.Missing);
        // Lock in the same student -> equipment order as checkout, then reload the loan.
        var student = await LockStudentAsync(db, loan.StudentId, ct);
        var item = await LockEquipmentAsync(db, loan.EquipmentId, ct);
        await db.Entry(loan).ReloadAsync(ct);
        if (loan.Status != BorrowingStatus.Active) return WriteResult.Rejected("This borrowing has already been returned.");
        if (today.Date < loan.BorrowedDate.Date) return WriteResult.Rejected("Return date cannot precede checkout.");
        if (student is null || item is null) return WriteResult.Rejected("A related record is missing. Refresh and retry.", WriteFailure.Missing);
        loan.Status = BorrowingStatus.Returned; loan.ActualReturnDate = today.Date;
        item.IsAvailable = true;
        await db.SaveChangesAsync(ct);
        return WriteResult.Saved("Equipment returned and recorded in history.", loan.Id) with
        {
            Borrowings = [Describe(loan, student, item)]
        };
    }, ct);

    private static LoanDetails Describe(Borrowing loan, Student student, Equipment equipment) =>
        new(loan.Id, student.Id, student.StudentNumber, student.Name, equipment.Id, equipment.Identifier, equipment.Name,
            loan.BorrowedDate, loan.ExpectedReturnDate, loan.Status, loan.ActualReturnDate);

    private static async Task<Student?> LockStudentAsync(EquipmentBorrowingDbContext db, int id, CancellationToken ct) =>
        (await db.Students.FromSqlInterpolated($"SELECT *, xmin FROM \"Students\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
    private static async Task<Equipment?> LockEquipmentAsync(EquipmentBorrowingDbContext db, int id, CancellationToken ct) =>
        (await db.Equipment.FromSqlInterpolated($"SELECT *, xmin FROM \"Equipment\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
    private static WriteResult Stale() => WriteResult.Rejected("This record changed since selection. Refresh, reselect, and review your changes.");

    private async Task<WriteResult> WriteAsync(Func<EquipmentBorrowingDbContext, Task<WriteResult>> operation, CancellationToken ct)
    {
        var committing = false;
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var result = await operation(db);
            if (!result.IsSuccess) return result; // disposal rolls back; tracked edits never escape this operation
            committing = true;
            await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException) { return Stale(); }
        catch (Exception ex) when (ex is PostgresException || ex.InnerException is PostgresException)
        {
            var pg = (ex as PostgresException ?? ex.InnerException as PostgresException)!;
            return WriteResult.Rejected(pg.SqlState switch {
                PostgresErrorCodes.UniqueViolation => "Duplicate identifier or concurrent checkout. Refresh and review current records.",
                PostgresErrorCodes.ForeignKeyViolation => "A related record is missing. Refresh and reselect.",
                PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NotNullViolation => "Required fields or date/state rules were violated. No changes saved.",
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected => "Conflicting operation. No changes saved. Refresh before retrying.",
                _ => "Database operation failed. Try again after service is restored."
            }, pg.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected => WriteFailure.Conflict,
                PostgresErrorCodes.ForeignKeyViolation => WriteFailure.Missing,
                PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NotNullViolation => WriteFailure.Invalid,
                _ => WriteFailure.Database
            });
        }
        catch (Exception ex) when (ex is NpgsqlException or DbUpdateException or TimeoutException or OperationCanceledException or System.IO.IOException)
        {
            return committing
                ? new(false, "Commit outcome is UNKNOWN. Do not resubmit. Reconnect and refresh, inspect records/history, then decide whether to retry.", true)
                : WriteResult.Rejected("Database operation failed. Try again after service is restored.", WriteFailure.Database);
        }
    }
}
