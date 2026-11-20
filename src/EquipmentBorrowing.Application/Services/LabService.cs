using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public sealed class LabService(ILabRepository repository, ICalendar calendar)
{
    public Task<IReadOnlyList<Student>> StudentsAsync(CancellationToken ct = default) => repository.StudentsAsync(ct);
    public Task<IReadOnlyList<Equipment>> EquipmentAsync(bool availableOnly = false, CancellationToken ct = default) => repository.EquipmentAsync(availableOnly, ct);
    public Task<IReadOnlyList<LoanDetails>> LoansAsync(LoanQuery query, CancellationToken ct = default) => repository.LoansAsync(query with { Search = query.Search.Trim() }, ct);
    public Task<WriteResult> SaveStudentAsync(StudentEdit edit, CancellationToken ct = default)
    {
        edit = edit with { Number = edit.Number.Trim(), Name = edit.Name.Trim(), Contact = Optional(edit.Contact) };
        if (string.IsNullOrWhiteSpace(edit.Number) || string.IsNullOrWhiteSpace(edit.Name))
            return Task.FromResult(WriteResult.Rejected("Student number and full name are required."));
        return repository.SaveStudentAsync(edit, ct);
    }
    public Task<WriteResult> SaveEquipmentAsync(EquipmentEdit edit, CancellationToken ct = default)
    {
        edit = edit with { Identifier = edit.Identifier.Trim(), Name = edit.Name.Trim(), Description = Optional(edit.Description) };
        if (string.IsNullOrWhiteSpace(edit.Identifier) || string.IsNullOrWhiteSpace(edit.Name))
            return Task.FromResult(WriteResult.Rejected("Equipment identifier and name are required."));
        return repository.SaveEquipmentAsync(edit, ct);
    }
    public Task<WriteResult> EligibilityAsync(int id, uint version, bool allowed, CancellationToken ct = default) => repository.EligibilityAsync(id, version, allowed, ct);
    public Task<WriteResult> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        var today = calendar.Today;
        var ids = request.EquipmentIds;
        string? error = request.StudentId <= 0 ? "Select a student." :
            ids.Count < (request.Multiple ? 2 : 1) || (!request.Multiple && ids.Count != 1) ?
                (request.Multiple ? "Select at least two distinct items." : "Select exactly one item.") :
            ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Count ? "Duplicate or invalid equipment selection." :
            request.DueDate is null || request.DueDate.Value.Date <= today ? "Expected return must be later than today's checkout date." : null;
        return error is null ? repository.CheckoutAsync(request with { DueDate = request.DueDate!.Value.Date }, today, ct)
            : Task.FromResult(WriteResult.Rejected(error));
    }
    public Task<WriteResult> ReturnAsync(int id, CancellationToken ct = default) => repository.ReturnAsync(id, calendar.Today, ct);
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
