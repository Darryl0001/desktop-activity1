using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using EquipmentBorrowing.Application;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Domain;

[assembly: AvaloniaTestApplication(typeof(EquipmentBorrowing.Tests.HeadlessAppBuilder))]

namespace EquipmentBorrowing.Tests;

public class HeadlessApp : Avalonia.Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark; }
}
public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<HeadlessApp>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
public class WorkspaceTests
{
    private sealed class Calendar : ICalendar { public DateTime Today => new(2026, 11, 20); }
    private sealed class FakeRepository : ILabRepository
    {
        public int Writes;
        public bool FailReads;
        public bool Unknown;
        public TaskCompletionSource<IReadOnlyList<LoanDetails>>? PendingHistory;
        public Task<IReadOnlyList<Student>> StudentsAsync(CancellationToken ct = default)
        {
            if (FailReads) throw new IOException("Connection failure");
            return Task.FromResult<IReadOnlyList<Student>>(new[] {
                new Student { Id = 1, StudentNumber = "2026-0001", Name = "Juan Dela Cruz", Version = 1 },
                new Student { Id = 2, StudentNumber = "2026-0002", Name = "Maria Santos", Version = 1 } });
        }
        public Task<IReadOnlyList<Equipment>> EquipmentAsync(bool availableOnly, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Equipment>>(new[] {
                new Equipment { Id = 1, Identifier = "EQ-001", Name = "Laptop", Description = "Portable computer", Version = 1 },
                new Equipment { Id = 2, Identifier = "EQ-002", Name = "Projector", Version = 1 } });
        public Task<IReadOnlyList<LoanDetails>> LoansAsync(LoanQuery query, CancellationToken ct = default) =>
            query.StudentId is not null && PendingHistory is not null ? PendingHistory.Task :
            Task.FromResult<IReadOnlyList<LoanDetails>>(Array.Empty<LoanDetails>());
        private Task<WriteResult> Write() { Writes++; return Task.FromResult(Unknown
            ? new WriteResult(false, "Commit outcome UNKNOWN. Refresh and review.", true)
            : WriteResult.Saved("Saved.")); }
        public Task<WriteResult> SaveStudentAsync(StudentEdit e, CancellationToken ct = default) => Write();
        public Task<WriteResult> SaveEquipmentAsync(EquipmentEdit e, CancellationToken ct = default) => Write();
        public Task<WriteResult> EligibilityAsync(int id, uint version, bool allowed, CancellationToken ct = default) => Write();
        public Task<WriteResult> CheckoutAsync(CheckoutRequest r, DateTime today, CancellationToken ct = default) => Write();
        public Task<WriteResult> ReturnAsync(int id, DateTime today, CancellationToken ct = default) => Write();
    }
    private static WorkbenchViewModel VM(FakeRepository repository) => new(new LabService(repository, new Calendar()));

    [Fact] public async Task UnknownOutcomeBlocksWritesUntilSuccessfulRefreshAndStaffReview()
    {
        var repository = new FakeRepository { Unknown = true };
        var vm = VM(repository);
        vm.StudentNumber = "X"; vm.StudentName = "Name";
        await vm.SaveStudentCommand.ExecuteAsync(null);
        Assert.True(vm.NeedsReview); Assert.False(vm.CanWrite);
        vm.RecordsReviewedCommand.Execute(null);
        Assert.True(vm.NeedsReview);
        await vm.SaveStudentCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.Writes);
        repository.FailReads = true;
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.RecordsReviewedCommand.Execute(null);
        Assert.True(vm.NeedsReview);
        repository.FailReads = false;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.NeedsReview); // Refresh never decides whether an uncertain request should be retried.
        vm.RecordsReviewedCommand.Execute(null);
        Assert.False(vm.NeedsReview); Assert.True(vm.CanWrite);
    }
    [Fact] public async Task AsyncHistoryShowsBusyStateAndDiscardsResultsAfterSelectionChanges()
    {
        var repository = new FakeRepository { PendingHistory = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm = VM(repository);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedStudent = vm.Students[0];
        var loading = vm.StudentHistoryCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy); Assert.False(vm.CanWrite); Assert.Empty(vm.StudentHistory);
        vm.SelectedStudent = vm.Students[1];
        repository.PendingHistory.SetResult(new[] { new LoanDetails(1, 1, "2026-0001", "Juan Dela Cruz", 1, "EQ-001", "Laptop",
            new(2026, 11, 20), new(2026, 11, 27), BorrowingStatus.Active, null) });
        await loading;
        Assert.False(vm.IsBusy); Assert.Empty(vm.StudentHistory);
        Assert.Contains("Selection changed", vm.StatusMessage);
    }
    [AvaloniaFact] public async Task AllWorkspaceTabsRenderAtNormalAndMinimumSizes()
    {
        var vm = VM(new FakeRepository());
        await vm.RefreshCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        try
        {
            var tabs = Assert.Single(window.GetVisualDescendants().OfType<TabControl>());
            Assert.Equal(3, tabs.ItemCount);
            var output = Environment.GetEnvironmentVariable("LAB4_UI_EVIDENCE_DIR");
            foreach (var (width, height) in new[] { (1180, 820), (900, 650) })
            foreach (var tab in new[] { 0, 1, 2 })
            {
                window.Width = width; window.Height = height; tabs.SelectedIndex = tab;
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Assert.True(frame.PixelSize.Width >= width);
                Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Content?.ToString() == "Reconnect / refresh");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Directory.CreateDirectory(output);
                    frame.Save(Path.Combine(output, $"workspace-{width}-{tab}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                }
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact] public async Task BoundDesktopCommandsPersistCheckoutHistorySearchAndReturn()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LAB4_TEST_CONNECTION_STRING")),
            "Set LAB4_TEST_CONNECTION_STRING for database-backed desktop integration.");
        await using var fixture = new PostgresWorkflowTests();
        await fixture.InitializeAsync();
        var vm = new WorkbenchViewModel(fixture.AppService);
        await vm.RefreshCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        try
        {
            var tabs = Assert.Single(window.GetVisualDescendants().OfType<TabControl>());
            tabs.SelectedIndex = 1;
            window.CaptureRenderedFrame()?.Dispose();
            void Input(string placeholder, string value)
            {
                var box = Assert.Single(window.GetVisualDescendants().OfType<TextBox>(), t => t.PlaceholderText == placeholder);
                box.SetCurrentValue(TextBox.TextProperty, value);
            }
            async Task Button(string label)
            {
                var button = Assert.Single(window.GetVisualDescendants().OfType<Button>(), b => b.Content?.ToString() == label);
                var command = Assert.IsAssignableFrom<CommunityToolkit.Mvvm.Input.IAsyncRelayCommand>(button.Command);
                await command.ExecuteAsync(null);
            }
            Input("Institutional student number (required)", "UI-001");
            Input("Full name (required)", "Desktop Integration Student");
            Assert.Equal("UI-001", vm.StudentNumber);
            await Button("Save student");
            Assert.NotNull(vm.SelectedStudent);
            Assert.Equal("UI-001", vm.SelectedStudent.StudentNumber);
            var studentId = vm.SelectedStudent.Id;
            tabs.SelectedIndex = 0;
            window.CaptureRenderedFrame()?.Dispose();
            vm.CheckoutStudent = vm.CheckoutStudents.Single(s => s.Id == studentId);
            var datePicker = Assert.Single(window.GetVisualDescendants().OfType<CalendarDatePicker>());
            var dueDate = new DateTime(2026, 11, 27);
            datePicker.SetCurrentValue(CalendarDatePicker.SelectedDateProperty, dueDate);
            Assert.Equal(dueDate, vm.DueDate);
            var checkout = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                b => b.Content?.ToString() == "Check out checked items (2–3)");
            Assert.False(checkout.IsEnabled);
            vm.Equipment[0].IsChecked = true;
            Assert.False(checkout.IsEnabled);
            vm.Equipment[1].IsChecked = true;
            Assert.True(checkout.IsEnabled);
            await Button("Check out checked items (2–3)");
            Assert.Contains("Checked out 2", vm.StatusMessage);
            Assert.Equal(2, vm.Loans.Count);
            tabs.SelectedIndex = 1;
            window.CaptureRenderedFrame()?.Dispose();
            await Button("Load selected student history");
            Assert.Equal(2, vm.StudentHistory.Count);
            tabs.SelectedIndex = 2;
            window.CaptureRenderedFrame()?.Dispose();
            Input("Search student name, student number, or equipment name", "  ui-001 ");
            await Button("Apply filters");
            Assert.Equal(2, vm.Loans.Count);
            vm.SelectedLoan = vm.Loans[0];
            await Button("Confirm return of selected borrowing");
            Assert.Contains("returned", vm.StatusMessage);
            Assert.Single(vm.Loans, b => b.Status == BorrowingStatus.Returned && b.ActualReturnDate is not null);
            Assert.False(vm.CanReturn);
            var output = Environment.GetEnvironmentVariable("LAB4_UI_EVIDENCE_DIR");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                using var frame = window.CaptureRenderedFrame();
                frame!.Save(Path.Combine(output, "database-workflow.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
        }
        finally { window.Close(); }
    }
}
