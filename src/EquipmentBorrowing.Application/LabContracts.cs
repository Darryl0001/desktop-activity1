using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application;

public enum LoanFilter { All, Active, Returned }
public enum LoanSort { Checkout, ExpectedReturn }
public record LoanQuery(string Search = "", LoanFilter Filter = LoanFilter.All,
    LoanSort Sort = LoanSort.Checkout, bool Descending = true, int? StudentId = null, int? EquipmentId = null, int? BorrowingId = null);
public record LoanDetails(int Id, int StudentId, string StudentNumber, string StudentName,
    int EquipmentId, string Identifier, string EquipmentName, DateTime BorrowedDate,
    DateTime ExpectedReturnDate, BorrowingStatus Status, DateTime? ActualReturnDate);
public record StudentEdit(int Id, string Number, string Name, string? Contact, uint Version = 0, string? Revision = null);
public record EquipmentEdit(int Id, string Identifier, string Name, string? Description, uint Version = 0, string? Revision = null);
public record CheckoutRequest(int StudentId, IReadOnlyList<int> EquipmentIds, DateTime? DueDate, bool Multiple);
public enum WriteFailure { Conflict, Invalid, Missing, Database }
public record WriteResult(bool IsSuccess, string Message, bool OutcomeUnknown = false, int? RecordId = null,
    WriteFailure Failure = WriteFailure.Conflict)
{
    public Student? Student { get; init; }
    public Equipment? Equipment { get; init; }
    public IReadOnlyList<LoanDetails>? Borrowings { get; init; }

    public static WriteResult Rejected(string message, WriteFailure failure = WriteFailure.Conflict) =>
        new(false, message, Failure: failure);
    public static WriteResult Saved(string message, int? id = null) => new(true, message, false, id);
}
// Shared workflow surface for the local regression tests and desktop HTTP adapter.
public interface ILabOperations
{
    Task<IReadOnlyList<Student>> StudentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Student>> SearchStudentsAsync(string search, CancellationToken ct = default);
    Task<IReadOnlyList<Equipment>> EquipmentAsync(bool availableOnly = false, CancellationToken ct = default);
    Task<IReadOnlyList<LoanDetails>> LoansAsync(LoanQuery query, CancellationToken ct = default);
    Task<WriteResult> SaveStudentAsync(StudentEdit edit, CancellationToken ct = default);
    Task<WriteResult> SaveEquipmentAsync(EquipmentEdit edit, CancellationToken ct = default);
    Task<WriteResult> EligibilityAsync(int id, uint version, bool allowed, CancellationToken ct = default, string? revision = null);
    Task<WriteResult> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default);
    Task<WriteResult> ReturnAsync(int id, CancellationToken ct = default);
}

public sealed class RecordMissingException(string message) : Exception(message);
public interface ICalendar { DateTime Today { get; } }

// A repository operation owns its context and transaction. No EF or provider types cross this boundary.
public interface ILabRepository
{
    Task<IReadOnlyList<Student>> StudentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Student>> SearchStudentsAsync(string search, CancellationToken ct = default);
    Task<Student?> StudentAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Equipment>> EquipmentAsync(bool availableOnly, CancellationToken ct = default);
    Task<Equipment?> EquipmentItemAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<LoanDetails>> LoansAsync(LoanQuery query, CancellationToken ct = default);
    Task<WriteResult> SaveStudentAsync(StudentEdit edit, CancellationToken ct = default);
    Task<WriteResult> SaveEquipmentAsync(EquipmentEdit edit, CancellationToken ct = default);
    Task<WriteResult> EligibilityAsync(int id, uint version, bool allowed, CancellationToken ct = default);
    Task<WriteResult> CheckoutAsync(CheckoutRequest request, DateTime today, CancellationToken ct = default);
    Task<WriteResult> ReturnAsync(int borrowingId, DateTime today, CancellationToken ct = default);
}
