using System.Text.Json;
using EquipmentBorrowing.Application;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Api;

internal static class ApiEndpoints
{
    public static void MapLabEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/equipment", ListEquipment);                                      // API-01
        api.MapGet("/students", ListStudents);                                        // API-02
        api.MapGet("/students/{id}", GetStudent);                                     // API-03
        api.MapPost("/students", (HttpContext ctx, LabService lab, CancellationToken ct) =>
            SaveStudent(0, ctx, lab, ct));                                             // API-04
        api.MapPut("/students/{id}", (int id, HttpContext ctx, LabService lab, CancellationToken ct) =>
            id > 0 ? SaveStudent(id, ctx, lab, ct) : Task.FromResult(Missing("Student"))); // API-05
        api.MapPatch("/students/{id}/eligibility", SetEligibility);                    // API-06
        api.MapGet("/equipment/{id}", GetEquipment);                                 // API-07
        api.MapPost("/equipment", (HttpContext ctx, LabService lab, CancellationToken ct) =>
            SaveEquipment(0, ctx, lab, ct));                                           // API-08
        api.MapPut("/equipment/{id}", (int id, HttpContext ctx, LabService lab, CancellationToken ct) =>
            id > 0 ? SaveEquipment(id, ctx, lab, ct) : Task.FromResult(Missing("Equipment"))); // API-09
        api.MapPost("/borrowings", BorrowOne);                                        // API-10
        api.MapPost("/checkout-requests", Checkout);                                  // API-11
        api.MapPost("/borrowings/{id}/return", Return);                               // API-12
        api.MapGet("/borrowings/{id}", GetBorrowing);                                 // API-13
        api.MapGet("/borrowings", ListBorrowings);                                    // API-14
        api.MapGet("/students/{id}/borrowings", (int id, LabService lab, CancellationToken ct) =>
            History(new LoanQuery(StudentId: id, Descending: false), lab, ct));        // API-15
        api.MapGet("/equipment/{id}/borrowings", (int id, LabService lab, CancellationToken ct) =>
            History(new LoanQuery(EquipmentId: id, Descending: false), lab, ct));      // API-16
    }

    private static async Task<IResult> ListEquipment(HttpRequest request, LabService lab, CancellationToken ct)
    {
        var available = false;
        if (!ValidQuery(request, "available") ||
            (Query(request, "available") is { } value && !bool.TryParse(value, out available)))
            return Invalid("The available query parameter must be true or false.");
        return Results.Ok((await lab.EquipmentAsync(available, ct)).Select(ToApi));
    }

    private static async Task<IResult> ListStudents(HttpRequest request, LabService lab, CancellationToken ct)
    {
        if (!ValidQuery(request, "search")) return Invalid("Provide a single student search value.");
        return Results.Ok((await lab.SearchStudentsAsync(Query(request, "search") ?? "", ct)).Select(ToApi));
    }

    private static async Task<IResult> GetStudent(int id, HttpResponse response, LabService lab, CancellationToken ct)
    {
        if (await lab.StudentAsync(id, ct) is not { } student) return Missing("Student");
        var value = ToApi(student);
        response.Headers.ETag = value.Revision();
        return Results.Ok(value);
    }

    private static async Task<IResult> GetEquipment(int id, HttpResponse response, LabService lab, CancellationToken ct)
    {
        if (await lab.EquipmentItemAsync(id, ct) is not { } item) return Missing("Equipment");
        var value = ToApi(item);
        response.Headers.ETag = value.Revision();
        return Results.Ok(value);
    }

    private static async Task<IResult> SaveStudent(int id, HttpContext context, LabService lab, CancellationToken ct)
    {
        var payload = id == 0
            ? await Read<StudentInput>(context.Request, ct)
            : await Read<StudentUpdateInput>(context.Request, ct) is { } update
                ? new StudentInput(update.StudentNumber, update.FullName, update.Contact)
                : null;
        if (payload is not { StudentNumber: { } number, FullName: { } name } input ||
            !ValidText(number, name, input.Contact) || (id == 0 && input.Contact is not null && string.IsNullOrWhiteSpace(input.Contact)))
            return Invalid("Student number and full name are required. Omit contact or use a nonblank value.");
        uint version = 0;
        if (id != 0)
        {
            if (await lab.StudentAsync(id, ct) is not { } current) return Missing("Student");
            if (!RevisionMatches(context.Request, ToApi(current).Revision())) return Stale();
            version = current.Version;
        }
        var result = await lab.SaveStudentAsync(new(id, number, name, input.Contact, version), ct);
        if (!result.IsSuccess) return Failure(result);
        var value = ToApi(result.Student!);
        context.Response.Headers.ETag = value.Revision();
        return id == 0 ? Results.Created($"/api/v1/students/{value.Id}", value) : Results.Ok(value);
    }

    private static async Task<IResult> SaveEquipment(int id, HttpContext context, LabService lab, CancellationToken ct)
    {
        var payload = id == 0
            ? await Read<EquipmentInput>(context.Request, ct)
            : await Read<EquipmentUpdateInput>(context.Request, ct) is { } update
                ? new EquipmentInput(update.EquipmentCode, update.Name, update.Description)
                : null;
        if (payload is not { EquipmentCode: { } code, Name: { } name } input ||
            !ValidText(code, name, input.Description))
            return Invalid("Equipment code and name are required. Only descriptive fields can be edited.");
        uint version = 0;
        if (id != 0)
        {
            if (await lab.EquipmentItemAsync(id, ct) is not { } current) return Missing("Equipment");
            if (!RevisionMatches(context.Request, ToApi(current).Revision())) return Stale();
            version = current.Version;
        }
        var result = await lab.SaveEquipmentAsync(new(id, code, name, input.Description, version), ct);
        if (!result.IsSuccess) return Failure(result);
        var value = ToApi(result.Equipment!);
        context.Response.Headers.ETag = value.Revision();
        return id == 0 ? Results.Created($"/api/v1/equipment/{value.Id}", value) : Results.Ok(value);
    }

    private static async Task<IResult> SetEligibility(int id, HttpContext context, LabService lab, CancellationToken ct)
    {
        if (await Read<EligibilityInput>(context.Request, ct) is not { IsEligible: bool allowed })
            return Invalid("Provide isEligible as a JSON boolean.");
        if (await lab.StudentAsync(id, ct) is not { } current) return Missing("Student");
        if (current.IsAllowedToBorrow != allowed && !RevisionMatches(context.Request, ToApi(current).Revision())) return Stale();
        var result = await lab.EligibilityAsync(id, current.Version, allowed, ct);
        if (!result.IsSuccess) return Failure(result);
        var value = ToApi(result.Student!);
        context.Response.Headers.ETag = value.Revision();
        return Results.Ok(value);
    }

    private static async Task<IResult> BorrowOne(HttpRequest request, LabService lab, CancellationToken ct)
    {
        if (await Read<BorrowingInput>(request, ct) is not { StudentId: int student, EquipmentId: int equipment, ExpectedReturnDate: { } due })
            return Invalid("Provide studentId, equipmentId, and expectedReturnDate as YYYY-MM-DD.");
        var result = await lab.CheckoutAsync(new(student, [equipment], due.ToDateTime(TimeOnly.MinValue), false), ct);
        if (!result.IsSuccess) return Failure(result);
        var value = ToApi(result.Borrowings!.Single());
        return Results.Created($"/api/v1/borrowings/{value.Id}", value);
    }

    private static async Task<IResult> Checkout(HttpRequest request, LabService lab, CancellationToken ct)
    {
        if (await Read<CheckoutInput>(request, ct) is not { StudentId: int student, EquipmentIds: { } equipment, ExpectedReturnDate: { } due })
            return Invalid("Provide studentId, equipmentIds, and expectedReturnDate as YYYY-MM-DD.");
        var result = await lab.CheckoutAsync(new(student, equipment, due.ToDateTime(TimeOnly.MinValue), true), ct);
        return result.IsSuccess
            ? Results.Json(new CheckoutOutput(result.Borrowings!.Select(ToApi).ToArray()), statusCode: 201)
            : Failure(result);
    }

    private static async Task<IResult> Return(int id, HttpRequest request, LabService lab, CancellationToken ct)
    {
        if (await Read<ReturnInput>(request, ct) is null) return Invalid("Send an empty JSON object for a return.");
        var result = await lab.ReturnAsync(id, ct);
        return result.IsSuccess ? Results.Ok(ToApi(result.Borrowings!.Single())) : Failure(result);
    }

    private static async Task<IResult> GetBorrowing(int id, LabService lab, CancellationToken ct)
    {
        var record = (await lab.LoansAsync(new(BorrowingId: id), ct)).SingleOrDefault();
        return record is null ? Missing("Borrowing") : Results.Ok(ToApi(record));
    }

    private static async Task<IResult> ListBorrowings(HttpRequest request, LabService lab, CancellationToken ct)
    {
        var filter = Query(request, "status") switch
        {
            null or "All" => LoanFilter.All,
            "Active" => LoanFilter.Active,
            "Returned" => LoanFilter.Returned,
            _ => (LoanFilter?)null
        };
        var sort = Query(request, "sort") switch
        {
            null or "borrowedDate" => LoanSort.Checkout,
            "expectedReturnDate" => LoanSort.ExpectedReturn,
            _ => (LoanSort?)null
        };
        var direction = Query(request, "direction");
        if (!ValidQuery(request, "search", "status", "sort", "direction") ||
            filter is null || sort is null || direction is not (null or "asc" or "desc"))
            return Invalid("Use a supported status, sort field, and direction.");
        var records = await lab.LoansAsync(new(Query(request, "search") ?? "", filter.Value, sort.Value, direction == "desc"), ct);
        return Results.Ok(records.Select(ToApi));
    }

    private static async Task<IResult> History(LoanQuery query, LabService lab, CancellationToken ct)
    {
        try
        {
            return Results.Ok((await lab.LoansAsync(query, ct)).Select(ToApi));
        }
        catch (RecordMissingException ex)
        {
            return Error(404, "not_found", ex.Message);
        }
    }

    private static async Task<T?> Read<T>(HttpRequest request, CancellationToken ct) where T : class
    {
        if (!request.HasJsonContentType()) return null;
        try { return await request.ReadFromJsonAsync<T>(ct); }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }
    private static bool RevisionMatches(HttpRequest request, string revision) =>
        !request.Headers.TryGetValue("If-Match", out var tags) ||
        tags.SelectMany(value => (value ?? "").Split(',')).Any(tag => tag.Trim() is "*" || tag.Trim() == revision);
    private static bool ValidText(params string?[] values) => values.All(value => value is null || !value.Contains('\0'));
    private static bool ValidQuery(HttpRequest request, params string[] names) =>
        request.Query.All(pair => names.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) &&
            pair.Value.Count == 1 && ValidText(pair.Value[0]));
    private static string? Query(HttpRequest request, string name) =>
        request.Query.TryGetValue(name, out var value) ? value.ToString() : null;

    private static IResult Missing(string subject) => Error(404, "not_found", $"{subject} record was not found. Refresh and reselect.");
    private static IResult Invalid(string message) => Error(400, "invalid_request", message);
    private static IResult Stale() => Error(409, "conflict", "This record changed since selection. Refresh, reselect, and review your changes.");
    internal static IResult Error(int status, string code, string message) =>
        Results.Json(new ApiError(code, message), statusCode: status);
    private static IResult Failure(WriteResult result) => result switch
    {
        { OutcomeUnknown: true } => Error(500, "outcome_unknown", result.Message),
        { Failure: WriteFailure.Invalid } => Invalid(result.Message),
        { Failure: WriteFailure.Missing } => Error(404, "not_found", result.Message),
        { Failure: WriteFailure.Database } => Error(500, "server_error", result.Message),
        _ => Error(409, "conflict", result.Message)
    };

    private static ApiStudent ToApi(Student student) =>
        new(student.Id, student.StudentNumber, student.Name, student.Contact, student.IsAllowedToBorrow);
    private static ApiEquipment ToApi(Equipment equipment) =>
        new(equipment.Id, equipment.Identifier, equipment.Name, equipment.Description, equipment.IsAvailable);
    private static ApiBorrowing ToApi(LoanDetails loan) => new(loan.Id, loan.StudentId, loan.EquipmentId,
        DateOnly.FromDateTime(loan.BorrowedDate), DateOnly.FromDateTime(loan.ExpectedReturnDate),
        loan.ActualReturnDate is { } returned ? DateOnly.FromDateTime(returned) : null,
        loan.Status == BorrowingStatus.Active ? ApiBorrowingStatus.Active : ApiBorrowingStatus.Returned,
        loan.StudentNumber, loan.StudentName, loan.Identifier, loan.EquipmentName);
}
