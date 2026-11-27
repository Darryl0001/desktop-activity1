using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EquipmentBorrowing.Application;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.Services;

public sealed class HttpLabClient(HttpClient http) : ILabOperations
{
    public async Task<IReadOnlyList<Student>> StudentsAsync(CancellationToken ct = default) =>
        await SearchStudentsAsync("", ct);

    public async Task<IReadOnlyList<Student>> SearchStudentsAsync(string search, CancellationToken ct = default) =>
        (await Get<ApiStudent[]>($"students?search={Uri.EscapeDataString(search)}", ct)).Select(ToStudent).ToArray();

    public async Task<IReadOnlyList<Equipment>> EquipmentAsync(bool availableOnly = false, CancellationToken ct = default) =>
        (await Get<ApiEquipment[]>(availableOnly ? "equipment?available=true" : "equipment", ct)).Select(ToEquipment).ToArray();

    public async Task<IReadOnlyList<LoanDetails>> LoansAsync(LoanQuery query, CancellationToken ct = default)
    {
        var sort = query.Sort == LoanSort.ExpectedReturn ? "expectedReturnDate" : "borrowedDate";
        var direction = query.Descending ? "desc" : "asc";
        var path = query.StudentId is int studentId ? $"students/{studentId}/borrowings" :
            query.EquipmentId is int equipmentId ? $"equipment/{equipmentId}/borrowings" :
            $"borrowings?search={Uri.EscapeDataString(query.Search)}&status={query.Filter}&sort={sort}&direction={direction}";
        return (await Get<ApiBorrowing[]>(path, ct)).Select(ToLoan).ToArray();
    }

    public Task<WriteResult> SaveStudentAsync(StudentEdit edit, CancellationToken ct = default)
    {
        var body = new StudentInput(edit.Number, edit.Name, BlankToNull(edit.Contact));
        return edit.Id == 0
            ? Send(ApiWrite<ApiStudent>("students", HttpMethod.Post, body, "Student registered.", value => value.Id), ct)
            : Send(ApiWrite<ApiStudent>($"students/{edit.Id}", HttpMethod.Put, body, "Student saved.", value => value.Id, edit.Revision), ct);
    }

    public Task<WriteResult> SaveEquipmentAsync(EquipmentEdit edit, CancellationToken ct = default)
    {
        var body = new EquipmentInput(edit.Identifier, edit.Name, BlankToNull(edit.Description));
        return edit.Id == 0
            ? Send(ApiWrite<ApiEquipment>("equipment", HttpMethod.Post, body, "Equipment registered.", value => value.Id), ct)
            : Send(ApiWrite<ApiEquipment>($"equipment/{edit.Id}", HttpMethod.Put, body, "Equipment saved.", value => value.Id, edit.Revision), ct);
    }

    public Task<WriteResult> EligibilityAsync(int id, uint version, bool allowed, CancellationToken ct = default, string? revision = null) =>
        Send(ApiWrite<ApiStudent>($"students/{id}/eligibility", HttpMethod.Patch, new EligibilityInput(allowed),
            allowed ? "Student is eligible to borrow." : "Student is restricted from new borrowing.", value => value.Id, revision), ct);

    public Task<WriteResult> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        var due = request.DueDate is { } date ? DateOnly.FromDateTime(date) : (DateOnly?)null;
        if (request.Multiple)
            return Send(ApiWrite<CheckoutOutput>("checkout-requests", HttpMethod.Post,
                new CheckoutInput(request.StudentId, request.EquipmentIds.ToArray(), due),
                $"Checked out {request.EquipmentIds.Count} items.", _ => null), ct);
        if (request.EquipmentIds.Count != 1)
            return Task.FromResult(WriteResult.Rejected("Select exactly one item.", WriteFailure.Invalid));
        return Send(ApiWrite<ApiBorrowing>("borrowings", HttpMethod.Post,
            new BorrowingInput(request.StudentId, request.EquipmentIds[0], due), "Checked out 1 item.", value => value.Id), ct);
    }

    public Task<WriteResult> ReturnAsync(int id, CancellationToken ct = default) =>
        Send(ApiWrite<ApiBorrowing>($"borrowings/{id}/return", HttpMethod.Post, new ReturnInput(),
            "Equipment returned and recorded in history.", value => value.Id), ct);

    private static ApiMutation<T> ApiWrite<T>(string path, HttpMethod method, object body,
        string feedback, Func<T, int?> recordId, string? revision = null) where T : class =>
        new(path, method, body, feedback, recordId, revision);

    private async Task<WriteResult> Send<T>(ApiMutation<T> operation, CancellationToken ct) where T : class
    {
        EnsureAddress();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(http.Timeout);
        ct = timeout.Token;
        using var request = new HttpRequestMessage(operation.Method, "api/v1/" + operation.Path)
        {
            Content = JsonContent.Create(operation.Body, operation.Body.GetType())
        };
        if (operation.Revision is not null) request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(operation.Revision));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode)
            {
                var payload = await Read<T>(response, ct);
                return WriteResult.Saved(operation.Feedback, operation.RecordId(payload));
            }

            var error = await ReadError(response, ct);
            if (error?.Code == "outcome_unknown")
                return Unknown($"API reported an unknown outcome (HTTP {(int)response.StatusCode}).");
            if ((int)response.StatusCode >= 500 && error?.Code != "server_error")
                return Unknown($"Server returned HTTP {(int)response.StatusCode} without a usable outcome.");
            return WriteResult.Rejected(Describe(response.StatusCode, error), response.StatusCode switch
            {
                HttpStatusCode.BadRequest => WriteFailure.Invalid,
                HttpStatusCode.NotFound => WriteFailure.Missing,
                HttpStatusCode.Conflict => WriteFailure.Conflict,
                _ => WriteFailure.Database
            });
        }
        catch (HttpRequestException ex) when (ConnectionWasRefused(ex))
        {
            return WriteResult.Rejected(Unreachable, WriteFailure.Database);
        }
        catch (Exception ex) when (IsTransportOrPayloadProblem(ex))
        {
            return Unknown("A complete API response could not be read.");
        }
    }

    private async Task<T> Get<T>(string path, CancellationToken ct) where T : class
    {
        EnsureAddress();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(http.Timeout);
        ct = timeout.Token;
        try
        {
            using var response = await http.GetAsync("api/v1/" + path, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode) return await Read<T>(response, ct);
            throw new ApiClientException(Describe(response.StatusCode, await ReadError(response, ct)));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            throw new ApiClientException(Unreachable);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ApiClientException("The API sent an unreadable response. Refresh to retry.");
        }
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct) where T : class =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw new JsonException("JSON body is missing.");

    private static async Task<ApiError?> ReadError(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await Read<ApiError>(response, ct); }
        catch (Exception ex) when (IsTransportOrPayloadProblem(ex)) { return null; }
    }

    private void EnsureAddress()
    {
        if (http.BaseAddress is null)
            throw new ApiClientException("LAB5_API_BASE_URL must be a valid HTTP or HTTPS address.");
    }

    private const string Unreachable = "Could not reach the API. No HTTP response was received. Check that the API is running and refresh.";
    private static WriteResult Unknown(string detail) => new(false,
        $"{detail} The write outcome is unknown. Reconnect, refresh, and review records before deciding whether to retry.", true);

    private static bool ConnectionWasRefused(HttpRequestException ex) =>
        ex.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.SecureConnectionError ||
        ex.GetBaseException() is SocketException { SocketErrorCode: SocketError.ConnectionRefused };

    private static bool IsTransportOrPayloadProblem(Exception ex) =>
        ex is HttpRequestException or OperationCanceledException or IOException or JsonException or NotSupportedException;

    private static string Describe(HttpStatusCode status, ApiError? error)
    {
        if ((int)status >= 500)
            return $"Server error (HTTP {(int)status}). {error?.Message ?? "Refresh after service is restored."}";
        return error?.Message ?? $"API request failed (HTTP {(int)status}). Check your selection and refresh.";
    }

    internal static string Revision(Student student) =>
        new ApiStudent(student.Id, student.StudentNumber, student.Name, student.Contact, student.IsAllowedToBorrow).Revision();
    internal static string Revision(Equipment equipment) =>
        new ApiEquipment(equipment.Id, equipment.Identifier, equipment.Name, equipment.Description, equipment.IsAvailable).Revision();
    private static Student ToStudent(ApiStudent value) => new()
    {
        Id = value.Id, StudentNumber = value.StudentNumber, Name = value.FullName,
        Contact = value.Contact, IsAllowedToBorrow = value.IsEligible
    };
    private static Equipment ToEquipment(ApiEquipment value) => new()
    {
        Id = value.Id, Identifier = value.EquipmentCode, Name = value.Name,
        Description = value.Description, IsAvailable = value.IsAvailable
    };
    private static LoanDetails ToLoan(ApiBorrowing value) =>
        new(value.Id, value.StudentId, value.StudentNumber, value.StudentName, value.EquipmentId, value.EquipmentCode, value.EquipmentName,
            value.BorrowedDate.ToDateTime(TimeOnly.MinValue), value.ExpectedReturnDate.ToDateTime(TimeOnly.MinValue),
            value.Status == ApiBorrowingStatus.Active ? BorrowingStatus.Active : BorrowingStatus.Returned,
            value.ReturnedDate?.ToDateTime(TimeOnly.MinValue));
    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record ApiMutation<T>(string Path, HttpMethod Method, object Body, string Feedback,
        Func<T, int?> RecordId, string? Revision) where T : class;
}

internal sealed class ApiClientException(string message) : Exception(message);
