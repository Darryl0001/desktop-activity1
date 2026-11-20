using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application;

public enum LoanFilter { All, Active, Returned }
public enum LoanSort { Checkout, ExpectedReturn }
public record LoanQuery(string Search = "", LoanFilter Filter = LoanFilter.All,
    LoanSort Sort = LoanSort.Checkout, bool Descending = true, int? StudentId = null, int? EquipmentId = null);
public record LoanDetails(int Id, int StudentId, string StudentNumber, string StudentName,
    int EquipmentId, string Identifier, string EquipmentName, DateTime BorrowedDate,
    DateTime ExpectedReturnDate, BorrowingStatus Status, DateTime? ActualReturnDate);
public record StudentEdit(int Id, string Number, string Name, string? Contact, uint Version = 0);
public record EquipmentEdit(int Id, string Identifier, string Name, string? Description, uint Version = 0);
public record CheckoutRequest(int StudentId, IReadOnlyList<int> EquipmentIds, DateTime? DueDate, bool Multiple);
public record WriteResult(bool IsSuccess, string Message, bool OutcomeUnknown = false, int? RecordId = null)
{
    public static WriteResult Rejected(string message) => new(false, message);
    public static WriteResult Saved(string message, int? id = null) => new(true, message, false, id);
}
public sealed class RecordMissingException(string message) : Exception(message);
public interface ICalendar { DateTime Today { get; } }

// A repository operation owns its context and transaction. No EF or provider types cross this boundary.
public interface ILabRepository
{
    Task<IReadOnlyList<Student>> StudentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Equipment>> EquipmentAsync(bool availableOnly, CancellationToken ct = default);
    Task<IReadOnlyList<LoanDetails>> LoansAsync(LoanQuery query, CancellationToken ct = default);
    Task<WriteResult> SaveStudentAsync(StudentEdit edit, CancellationToken ct = default);
    Task<WriteResult> SaveEquipmentAsync(EquipmentEdit edit, CancellationToken ct = default);
    Task<WriteResult> EligibilityAsync(int id, uint version, bool allowed, CancellationToken ct = default);
    Task<WriteResult> CheckoutAsync(CheckoutRequest request, DateTime today, CancellationToken ct = default);
    Task<WriteResult> ReturnAsync(int borrowingId, DateTime today, CancellationToken ct = default);
}
