using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application;
using EquipmentBorrowing.Desktop.Services;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class EquipmentRow(Equipment item) : ObservableObject
{
    public Equipment Item { get; } = item;
    public string Availability => Item.IsAvailable ? "Available" : "On loan";
    [ObservableProperty] private bool isChecked;
}
public partial class WorkbenchViewModel : ObservableObject
{
    private readonly ILabOperations service;
    public WorkbenchViewModel(ILabOperations service)
    {
        this.service = service;
        Students.CollectionChanged += (_, _) => OnPropertyChanged(nameof(StudentState));
        Equipment.CollectionChanged += EquipmentChanged;
        Loans.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LoanState));
        StudentHistory.CollectionChanged += (_, _) => OnPropertyChanged(nameof(StudentHistoryState));
        EquipmentHistory.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EquipmentHistoryState));
    }
    public string StudentState => Students.Count == 1 ? "1 student" : $"{Students.Count} students";
    public string CatalogState => Equipment.Count == 1 ? "1 item" : $"{Equipment.Count} items";
    public string LoanState => Loans.Count == 1 ? "1 borrowing" : $"{Loans.Count} borrowings";
    public bool ShowEmptyStudents => IsIdle && studentsLoaded && Students.Count == 0;
    public bool ShowEmptyCatalog => IsIdle && equipmentLoaded && Equipment.Count == 0;
    public bool ShowEmptyLoans => IsIdle && loansLoaded && Loans.Count == 0;
    public string StudentHistoryState => HistoryState(studentHistoryLoaded, StudentHistory.Count, "Select a student and open their borrowing history.");
    public string EquipmentHistoryState => HistoryState(equipmentHistoryLoaded, EquipmentHistory.Count, "Select an item and open its borrowing history.");
    public string StudentFormTitle => SelectedStudent is null ? "Register student" : "Edit student";
    public string EquipmentFormTitle => SelectedEquipment is null ? "Register equipment" : "Edit equipment";
    public bool HasStudent => SelectedStudent is not null;
    public bool HasEquipment => SelectedEquipment is not null;
    public bool CanChangeEligibility => CanWrite && HasStudent;
    public bool CanReturn => CanWrite && SelectedLoan?.Status == BorrowingStatus.Active;
    public bool CanBorrowOne => CanWrite && CheckoutStudent is not null && DueDate is not null && SelectedEquipment?.Item.IsAvailable == true;
    public bool CanCheckoutMultiple => CanWrite && CheckoutStudent is not null && DueDate is not null && CheckedItemCount is 2 or 3;
    public int CheckedItemCount => Equipment.Count(e => e.IsChecked);
    public string CheckoutSelection => CheckedItemCount == 0
        ? "Tick 2–3 items for a batch, or highlight one for a single checkout."
        : $"{CheckedItemCount} checked · Select 2–3 items for a batch.";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool needsReview;
    [ObservableProperty] private string statusMessage = "Refresh to connect to the borrowing API.";
    [ObservableProperty] private string studentSearch = "";
    [ObservableProperty] private string equipmentSearch = "";
    [ObservableProperty] private bool availableOnly;
    [ObservableProperty] private Student? selectedStudent;
    [ObservableProperty] private Student? checkoutStudent;
    [ObservableProperty] private EquipmentRow? selectedEquipment;
    [ObservableProperty] private LoanDetails? selectedLoan;
    [ObservableProperty] private string studentNumber = "";
    [ObservableProperty] private string studentName = "";
    [ObservableProperty] private string studentContact = "";
    [ObservableProperty] private string equipmentIdentifier = "";
    [ObservableProperty] private string equipmentName = "";
    [ObservableProperty] private string equipmentDescription = "";
    [ObservableProperty] private DateTime? dueDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")).Date.AddDays(7);
    [ObservableProperty] private string search = "";
    [ObservableProperty] private LoanFilter filter = LoanFilter.All;
    [ObservableProperty] private LoanSort sort = LoanSort.Checkout;
    [ObservableProperty] private bool descending;
    public ObservableCollection<Student> Students { get; } = new();
    public ObservableCollection<Student> CheckoutStudents { get; } = new();
    public ObservableCollection<EquipmentRow> Equipment { get; } = new();
    public ObservableCollection<LoanDetails> Loans { get; } = new();
    public ObservableCollection<LoanDetails> StudentHistory { get; } = new();
    public ObservableCollection<LoanDetails> EquipmentHistory { get; } = new();
    public LoanFilter[] Filters { get; } = Enum.GetValues<LoanFilter>();
    public LoanSort[] Sorts { get; } = Enum.GetValues<LoanSort>();
    public bool CanWrite => !IsBusy && !NeedsReview;
    public bool IsIdle => !IsBusy;
    private void EquipmentChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (EquipmentRow row in e.OldItems) row.PropertyChanged -= CheckedChanged;
        if (e.NewItems is not null)
            foreach (EquipmentRow row in e.NewItems) row.PropertyChanged += CheckedChanged;
        OnPropertyChanged(nameof(CatalogState));
        OnPropertyChanged(nameof(ShowEmptyCatalog));
        NotifyCheckoutSelection();
    }
    private void CheckedChanged(object? sender, PropertyChangedEventArgs e) => NotifyCheckoutSelection();
    private void NotifyCheckoutSelection()
    {
        OnPropertyChanged(nameof(CheckoutSelection));
        OnPropertyChanged(nameof(CanCheckoutMultiple));
    }
    private void NotifyWriteActions()
    {
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(CanChangeEligibility));
        OnPropertyChanged(nameof(CanReturn));
        OnPropertyChanged(nameof(CanBorrowOne));
        OnPropertyChanged(nameof(CanCheckoutMultiple));
    }
    partial void OnIsBusyChanged(bool value)
    {
        NotifyWriteActions();
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(ShowEmptyStudents));
        OnPropertyChanged(nameof(ShowEmptyCatalog));
        OnPropertyChanged(nameof(ShowEmptyLoans));
    }
    partial void OnNeedsReviewChanged(bool value) => NotifyWriteActions();
    partial void OnCheckoutStudentChanged(Student? value) => NotifyWriteActions();
    partial void OnDueDateChanged(DateTime? value) => NotifyWriteActions();
    partial void OnSelectedLoanChanged(LoanDetails? value) => OnPropertyChanged(nameof(CanReturn));
    partial void OnSelectedStudentChanged(Student? value)
    {
        studentHistoryLoaded = false;
        StudentHistory.Clear();
        StudentNumber = value?.StudentNumber ?? ""; StudentName = value?.Name ?? ""; StudentContact = value?.Contact ?? "";
        OnPropertyChanged(nameof(StudentFormTitle));
        OnPropertyChanged(nameof(HasStudent));
        OnPropertyChanged(nameof(CanChangeEligibility));
    }
    partial void OnSelectedEquipmentChanged(EquipmentRow? value)
    {
        equipmentHistoryLoaded = false;
        EquipmentHistory.Clear();
        EquipmentIdentifier = value?.Item.Identifier ?? ""; EquipmentName = value?.Item.Name ?? ""; EquipmentDescription = value?.Item.Description ?? "";
        OnPropertyChanged(nameof(EquipmentFormTitle));
        OnPropertyChanged(nameof(HasEquipment));
        OnPropertyChanged(nameof(CanBorrowOne));
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; StatusMessage = "Working…";
        try { await action(); }
        catch (RecordMissingException ex) { StatusMessage = ex.Message; }
        catch (ApiClientException ex) { StatusMessage = ex.Message; }
        catch (Exception) { StatusMessage = "Could not refresh records. Check the API configuration and retry."; }
        finally { IsBusy = false; }
    }
    [RelayCommand] private Task RefreshAsync() => RunAsync(async () => {
        await ReloadAsync();
        StatusMessage = NeedsReview
            ? "Reconnected and refreshed. Inspect the borrowing list and relevant histories. Then click 'Records reviewed' before deciding to retry."
            : "Connected. Records refreshed.";
    });
    private async Task ReloadAsync()
    {
        var selectedStudentId = SelectedStudent?.Id;
        var selectedItemId = SelectedEquipment?.Item.Id;
        var reloadStudentHistory = studentHistoryLoaded;
        var reloadEquipmentHistory = equipmentHistoryLoaded;
        var studentsTask = service.StudentsAsync();
        var studentResultsTask = string.IsNullOrWhiteSpace(StudentSearch) ? studentsTask : service.SearchStudentsAsync(StudentSearch);
        var equipmentTask = service.EquipmentAsync(AvailableOnly);
        var loansTask = service.LoansAsync(new(Search, Filter, Sort, Descending));
        await Task.WhenAll(studentsTask, studentResultsTask, equipmentTask, loansTask);
        var students = await studentsTask;
        var equipment = await equipmentTask;
        var loans = await loansTask;
        var checkedIds = Equipment.Where(e => e.IsChecked).Select(e => e.Item.Id).ToHashSet();
        var selectedLoanId = SelectedLoan?.Id;
        var checkoutId = CheckoutStudent?.Id;
        SelectedStudent = null; SelectedEquipment = null; SelectedLoan = null; CheckoutStudent = null;
        Replace(CheckoutStudents, students);
        Replace(Students, await studentResultsTask);
        ReplaceEquipment(equipment.Where(e => Matches(EquipmentSearch, e.Name, e.Identifier))
            .Select(e => new EquipmentRow(e) { IsChecked = e.IsAvailable && checkedIds.Contains(e.Id) }));
        Replace(Loans, loans);
        studentsLoaded = equipmentLoaded = loansLoaded = true;
        SelectedStudent = Students.FirstOrDefault(s => s.Id == selectedStudentId);
        SelectedEquipment = Equipment.FirstOrDefault(e => e.Item.Id == selectedItemId);
        CheckoutStudent = CheckoutStudents.FirstOrDefault(s => s.Id == checkoutId);
        SelectedLoan = Loans.FirstOrDefault(loan => loan.Id == selectedLoanId);
        if (reloadStudentHistory && SelectedStudent is { } student)
        {
            Replace(StudentHistory, await service.LoansAsync(new(StudentId: student.Id)));
            studentHistoryLoaded = true;
            OnPropertyChanged(nameof(StudentHistoryState));
        }
        if (reloadEquipmentHistory && SelectedEquipment is { } item)
        {
            Replace(EquipmentHistory, await service.LoansAsync(new(EquipmentId: item.Item.Id)));
            equipmentHistoryLoaded = true;
            OnPropertyChanged(nameof(EquipmentHistoryState));
        }
        recoveryRefreshed = true;
    }
    private bool recoveryRefreshed;
    private bool studentsLoaded;
    private bool equipmentLoaded;
    private bool loansLoaded;
    private bool studentHistoryLoaded;
    private bool equipmentHistoryLoaded;
    [RelayCommand] private void RecordsReviewed()
    {
        if (!NeedsReview) { StatusMessage = "No unknown operation is pending."; return; }
        if (!recoveryRefreshed) { StatusMessage = "Reconnect and refresh successfully before reviewing records."; return; }
        NeedsReview = false; StatusMessage = "Review acknowledged. You may decide whether a corrected request is needed.";
    }
    [RelayCommand] private void NewStudent() { SelectedStudent = null; StudentNumber = ""; StudentName = ""; StudentContact = ""; StudentHistory.Clear(); }
    [RelayCommand] private void NewEquipment() { SelectedEquipment = null; EquipmentIdentifier = ""; EquipmentName = ""; EquipmentDescription = ""; EquipmentHistory.Clear(); }
    [RelayCommand] private Task FindStudentsAsync() => RunAsync(async () => {
        var items = await service.SearchStudentsAsync(StudentSearch); SelectedStudent = null;
        Replace(Students, items);
        studentsLoaded = true;
        StatusMessage = Students.Count == 0 ? "No students match." : $"{Students.Count} student(s).";
    });
    [RelayCommand] private Task FindEquipmentAsync() => RunAsync(async () => {
        var items = await service.EquipmentAsync(AvailableOnly); SelectedEquipment = null;
        var checkedIds = Equipment.Where(e => e.IsChecked).Select(e => e.Item.Id).ToHashSet();
        ReplaceEquipment(items.Where(e => Matches(EquipmentSearch, e.Name, e.Identifier))
            .Select(e => new EquipmentRow(e) { IsChecked = e.IsAvailable && checkedIds.Contains(e.Id) }));
        equipmentLoaded = true;
        StatusMessage = Equipment.Count == 0 ? "No equipment matches." : $"{Equipment.Count} equipment item(s).";
    });
    [RelayCommand] private Task SaveStudentAsync() => WriteAsync(() =>
    {
        var selected = SelectedStudent;
        return service.SaveStudentAsync(new(selected?.Id ?? 0, StudentNumber, StudentName, StudentContact,
            selected?.Version ?? 0, selected is null ? null : HttpLabClient.Revision(selected)));
    }, "student");

    [RelayCommand] private Task SaveEquipmentAsync() => WriteAsync(() =>
    {
        var selected = SelectedEquipment?.Item;
        return service.SaveEquipmentAsync(new(selected?.Id ?? 0, EquipmentIdentifier, EquipmentName, EquipmentDescription,
            selected?.Version ?? 0, selected is null ? null : HttpLabClient.Revision(selected)));
    }, "equipment");
    [RelayCommand] private Task PermitAsync() => SetEligibilityAsync(true);
    [RelayCommand] private Task RestrictAsync() => SetEligibilityAsync(false);
    private Task SetEligibilityAsync(bool allowed) => SelectedStudent is null ? Feedback("Select a student first.") :
        WriteAsync(() => service.EligibilityAsync(SelectedStudent.Id, SelectedStudent.Version, allowed,
            revision: HttpLabClient.Revision(SelectedStudent)));
    [RelayCommand] private Task BorrowOneAsync() => SelectedEquipment is null ? Feedback("Select an equipment row first.") :
        CheckoutAsync(new[] { SelectedEquipment.Item.Id }, false);
    [RelayCommand] private Task CheckoutMultipleAsync() => CheckoutAsync(Equipment.Where(e => e.IsChecked).Select(e => e.Item.Id).ToArray(), true);
    private Task CheckoutAsync(int[] ids, bool multiple) => WriteAsync(() => service.CheckoutAsync(new(
        CheckoutStudent?.Id ?? 0, ids, DueDate?.Date, multiple)));
    [RelayCommand] private Task ReturnAsync() => SelectedLoan is null ? Feedback("Select a borrowing to return first.") :
        WriteAsync(() => service.ReturnAsync(SelectedLoan.Id));
    private Task WriteAsync(Func<Task<WriteResult>> action, string? savedKind = null)
    {
        if (NeedsReview) return Feedback("Outcome unknown. Refresh and review records before any further writes.");
        return RunAsync(async () => {
            var result = await action();
            if (result.OutcomeUnknown) { NeedsReview = true; recoveryRefreshed = false; }
            if (!result.OutcomeUnknown && (result.IsSuccess || result.Failure == WriteFailure.Missing ||
                (savedKind is null && result.Failure == WriteFailure.Conflict)))
            {
                if (result.IsSuccess && savedKind == "student") StudentSearch = "";
                if (result.IsSuccess && savedKind == "equipment") { EquipmentSearch = ""; AvailableOnly = false; }
                try { await ReloadAsync(); }
                catch (ApiClientException ex) { StatusMessage = result.Message + " Refresh failed. " + ex.Message; return; }
                catch { StatusMessage = result.Message + " Refresh failed. Check the API and reload."; return; }
                if (result.IsSuccess && savedKind == "student" && SelectedStudent?.Id != result.RecordId)
                    SelectedStudent = Students.FirstOrDefault(s => s.Id == result.RecordId);
                if (result.IsSuccess && savedKind == "equipment" && SelectedEquipment?.Item.Id != result.RecordId)
                    SelectedEquipment = Equipment.FirstOrDefault(e => e.Item.Id == result.RecordId);
            }
            StatusMessage = result.Message;
        });
    }
    [RelayCommand] private Task SearchLoansAsync() => RunAsync(async () => {
        var records = await service.LoansAsync(new(Search, Filter, Sort, Descending));
        SelectedLoan = null;
        Replace(Loans, records);
        loansLoaded = true;
        StatusMessage = Loans.Count == 0 ? "No borrowing records match." : $"{Loans.Count} borrowing record(s).";
    });
    [RelayCommand] private Task ActiveLoansAsync() { Search = ""; Filter = LoanFilter.Active; return SearchLoansAsync(); }
    [RelayCommand] private Task ClearCriteriaAsync() { Search = ""; Filter = LoanFilter.All; Sort = LoanSort.Checkout; Descending = false; return SearchLoansAsync(); }
    [RelayCommand] private Task StudentHistoryAsync() => RunAsync(async () => {
        if (SelectedStudent is null) { StatusMessage = "Select a student first."; return; }
        var id = SelectedStudent.Id;
        var records = await service.LoansAsync(new(StudentId: id));
        if (SelectedStudent?.Id != id) { StatusMessage = "Selection changed. Load the selected student's history."; return; }
        Replace(StudentHistory, records);
        studentHistoryLoaded = true;
        OnPropertyChanged(nameof(StudentHistoryState));
        StatusMessage = StudentHistory.Count == 0 ? "This student has no borrowing history." : $"{StudentHistory.Count} student history record(s).";
    });
    [RelayCommand] private Task EquipmentHistoryAsync() => RunAsync(async () => {
        if (SelectedEquipment is null) { StatusMessage = "Select equipment first."; return; }
        var id = SelectedEquipment.Item.Id;
        var records = await service.LoansAsync(new(EquipmentId: id));
        if (SelectedEquipment?.Item.Id != id) { StatusMessage = "Selection changed. Load the selected equipment's history."; return; }
        Replace(EquipmentHistory, records);
        equipmentHistoryLoaded = true;
        OnPropertyChanged(nameof(EquipmentHistoryState));
        StatusMessage = EquipmentHistory.Count == 0 ? "This equipment has no borrowing history." : $"{EquipmentHistory.Count} equipment history record(s).";
    });
    private static string HistoryState(bool loaded, int count, string prompt) => !loaded ? prompt : count switch
    {
        0 => "No borrowing history.",
        1 => "1 record",
        _ => $"{count} records"
    };

    private Task Feedback(string message) { StatusMessage = message; return Task.CompletedTask; }
    private void ReplaceEquipment(System.Collections.Generic.IEnumerable<EquipmentRow> source)
    {
        foreach (var row in Equipment) row.PropertyChanged -= CheckedChanged;
        Replace(Equipment, source);
    }
    private static bool Matches(string text, params string[] values) => values.Any(v => v.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase));
    private static void Replace<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> source)
    { target.Clear(); foreach (var item in source) target.Add(item); }
}
