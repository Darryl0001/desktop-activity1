using System.Net.Http.Json;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Desktop;
using EquipmentBorrowing.Desktop.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Domain;
using Npgsql;

namespace EquipmentBorrowing.Tests;

public class Lab5DesktopAcceptance
{
    [AvaloniaFact]
    public async Task HttpWorkflows()
    {
        var baseUrl = Environment.GetEnvironmentVariable("LAB5_TEST_BASE_URL");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(baseUrl), "Run scripts/verify-lab5.ps1 for the live HTTP desktop test.");
        using var http = new HttpClient(new EvidenceHandler()) { BaseAddress = new(baseUrl!), Timeout = TimeSpan.FromSeconds(8) };
        var vm = new WorkbenchViewModel(new HttpLabClient(http));
        var window = new MainWindow { DataContext = vm, Width = 1180, Height = 820 };
        window.Show();
        try
        {
            var tabs = Assert.Single(window.GetVisualDescendants().OfType<TabControl>());
            void Tab(int index)
            {
                tabs.SelectedIndex = index;
                window.CaptureRenderedFrame()?.Dispose();
            }
            async Task Click(string label)
            {
                var button = Assert.Single(window.GetVisualDescendants().OfType<Button>(), b => b.Content?.ToString() == label);
                Assert.True(button.IsEnabled);
                await Assert.IsAssignableFrom<IAsyncRelayCommand>(button.Command).ExecuteAsync(null);
                EvidenceHandler.Record(new { button = label, uiStatus = vm.StatusMessage });
            }

            await Click("Reconnect / refresh");
            Assert.Equal(4, vm.Students.Count);
            Assert.Equal(7, vm.Equipment.Count);
            Tab(1);
            vm.StudentNumber = "ST-UI";
            vm.StudentName = "Lab 5 Desktop Student";
            await Click("Save student");
            var studentId = Assert.IsType<Student>(vm.SelectedStudent).Id;
            vm.StudentName = "Lab 5 Desktop Student Edited";
            vm.StudentContact = "lab5-demo";
            await Click("Save student");
            Assert.Equal("Lab 5 Desktop Student Edited", vm.SelectedStudent?.Name);
            await Click("Load selected student history");
            Assert.Empty(vm.StudentHistory);

            Tab(0);
            vm.EquipmentIdentifier = "EQ-UI";
            vm.EquipmentName = "Desktop Test Kit";
            await Click("Save equipment");
            var equipmentId = Assert.IsType<EquipmentRow>(vm.SelectedEquipment).Item.Id;
            vm.EquipmentName = "Edited Desktop Kit";
            await Click("Save equipment");
            Assert.Equal("Edited Desktop Kit", vm.SelectedEquipment?.Item.Name);
            await Click("Load selected equipment history");
            Assert.Empty(vm.EquipmentHistory);
            vm.CheckoutStudent = vm.CheckoutStudents.Single(s => s.Id == studentId);
            vm.DueDate = new DateTime(2026, 11, 12);
            await Click("Borrow selected row");
            Assert.False(vm.Equipment.Single(e => e.Item.Id == equipmentId).Item.IsAvailable);
            Assert.Equal(BorrowingStatus.Active, Assert.Single(vm.EquipmentHistory).Status);
            SaveFrame(window, "T07-catalog-after-checkout");

            Tab(2);
            vm.SelectedLoan = vm.Loans.Single(loan => loan.StudentId == studentId);
            await Click("Confirm return of selected borrowing");
            Assert.True(vm.Equipment.Single(e => e.Item.Id == equipmentId).Item.IsAvailable);
            Assert.Equal(BorrowingStatus.Returned, Assert.Single(vm.EquipmentHistory).Status);
            Assert.Equal(BorrowingStatus.Returned, Assert.Single(vm.StudentHistory).Status);
            SaveFrame(window, "T07-returned-borrowing");

            // Simulate another client changing a record while the desktop remains open.
            using var independent = new HttpClient(new EvidenceHandler()) { BaseAddress = new(baseUrl!) };
            var change = new StudentInput("ST-UI", "Changed by independent HTTP client", "lab5-demo");
            using var response = await independent.PutAsJsonAsync($"api/v1/students/{studentId}", change);
            response.EnsureSuccessStatusCode();
            Assert.Equal("Lab 5 Desktop Student Edited", vm.Students.Single(s => s.Id == studentId).Name);
            await Click("Reconnect / refresh");
            Assert.Equal("Changed by independent HTTP client", vm.Students.Single(s => s.Id == studentId).Name);
            Tab(1);
            SaveFrame(window, "T07-refreshed-student-history");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ApiFailureFeedback()
    {
        var baseUrl = Environment.GetEnvironmentVariable("LAB5_TEST_BASE_URL");
        var expected = Environment.GetEnvironmentVariable("LAB5_UI_FAILURE");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(expected),
            "Run scripts/verify-lab5.ps1 for the live HTTP and transport cases.");
        using var http = new HttpClient(new EvidenceHandler()) { BaseAddress = new(baseUrl!), Timeout = TimeSpan.FromSeconds(8) };
        var vm = new WorkbenchViewModel(new HttpLabClient(http));
        var window = new MainWindow { DataContext = vm, Width = 1180, Height = 820 };
        window.Show();
        try
        {
            await vm.RefreshCommand.ExecuteAsync(null);
            Assert.False(vm.IsBusy);
            Assert.False(vm.ShowEmptyCatalog);
            var message = expected switch
            {
                "unreachable" => "No HTTP response",
                "server" => "Server error (HTTP 500)",
                "recovered" => "Connected. Records refreshed.",
                _ => throw new InvalidOperationException("Unknown acceptance phase.")
            };
            Assert.Contains(message, vm.StatusMessage);
            Assert.DoesNotContain("Npgsql", vm.StatusMessage);
            EvidenceHandler.Record(new { expected, uiStatus = vm.StatusMessage });
            SaveFrame(window, $"T08-{expected}");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task UnknownWriteResponseRequiresReview()
    {
        var baseUrl = Environment.GetEnvironmentVariable("LAB5_TEST_BASE_URL");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(baseUrl), "Run scripts/verify-lab5.ps1 for the response-loss case.");
        using var http = new HttpClient(new EvidenceHandler(dropFirstWriteResponse: true)) { BaseAddress = new(baseUrl!) };
        var vm = new WorkbenchViewModel(new HttpLabClient(http));
        var window = new MainWindow { DataContext = vm, Width = 1180, Height = 820 };
        window.Show();
        try
        {
            await vm.RefreshCommand.ExecuteAsync(null);
            vm.StudentNumber = "ST-LOST";
            vm.StudentName = "Committed Before Response Loss";
            await vm.SaveStudentCommand.ExecuteAsync(null);
            Assert.True(vm.NeedsReview);
            Assert.False(vm.CanWrite);
            Assert.Contains("write outcome is unknown", vm.StatusMessage);
            EvidenceHandler.Record(new { uiStatus = vm.StatusMessage, vm.NeedsReview, vm.CanWrite });
            SaveFrame(window, "T08-lost-write-response");
            vm.RecordsReviewedCommand.Execute(null);
            Assert.True(vm.NeedsReview);
            await vm.SaveStudentCommand.ExecuteAsync(null);
            Assert.True(vm.NeedsReview);

            await vm.RefreshCommand.ExecuteAsync(null);
            Assert.True(vm.NeedsReview);
            Assert.Single(vm.Students, student => student.StudentNumber == "ST-LOST");
            vm.RecordsReviewedCommand.Execute(null);
            Assert.True(vm.CanWrite);
            EvidenceHandler.Record(new { uiStatus = vm.StatusMessage, vm.NeedsReview, vm.CanWrite });
        }
        finally { window.Close(); }
    }

    private static void SaveFrame(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("LAB5_UI_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private sealed class EvidenceHandler(bool dropFirstWriteResponse = false) : DelegatingHandler(new HttpClientHandler())
    {
        private static readonly object Sync = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var mutation = request.Method != HttpMethod.Get;
            var before = mutation ? await Snapshot(ct) : null;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

            HttpResponseMessage response;
            try { response = await base.SendAsync(request, ct); }
            catch (HttpRequestException exception)
            {
                Record(new { method = request.Method.Method, path = request.RequestUri?.PathAndQuery, transport = exception.HttpRequestError.ToString() });
                throw;
            }

            var after = mutation ? await Snapshot(ct) : null;
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var responseDropped = mutation && dropFirstWriteResponse && response.IsSuccessStatusCode;
            Record(new { method = request.Method.Method, path = request.RequestUri?.PathAndQuery,
                body, status = (int)response.StatusCode, response = responseBody, before, after, responseDropped });
            if (!responseDropped) return response;
            dropFirstWriteResponse = false;
            response.Dispose();
            throw new HttpRequestException(HttpRequestError.ResponseEnded, "Lab 5 test dropped the committed response.");

        }

        private static async Task<JsonElement?> Snapshot(CancellationToken ct)
        {
            var connectionString = Environment.GetEnvironmentVariable("LAB5_TEST_CONNECTION_STRING");
            var sql = Environment.GetEnvironmentVariable("LAB5_SNAPSHOT_SQL");
            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(sql)) return null;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(sql, ct), connection);
            using var document = JsonDocument.Parse((string)(await command.ExecuteScalarAsync(ct))!);
            return document.RootElement.Clone();
        }

        public static void Record(object entry)
        {
            var file = Environment.GetEnvironmentVariable("LAB5_UI_TRACE_FILE");
            if (string.IsNullOrWhiteSpace(file)) return;
            lock (Sync)
            {
                File.AppendAllText(file, JsonSerializer.Serialize(entry) + Environment.NewLine);
            }
        }
    }
}
