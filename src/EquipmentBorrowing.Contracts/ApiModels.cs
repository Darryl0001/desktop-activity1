using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EquipmentBorrowing.Contracts;

public sealed record ApiStudent(int Id, string StudentNumber, string FullName, string? Contact, bool IsEligible)
{
    public string Revision() => ApiRevision.Of(this);
}
public sealed record ApiEquipment(int Id, string EquipmentCode, string Name, string? Description, bool IsAvailable)
{
    public string Revision() => ApiRevision.Of(this);
}
public sealed record ApiBorrowing(int Id, int StudentId, int EquipmentId,
    DateOnly BorrowedDate, DateOnly ExpectedReturnDate, DateOnly? ReturnedDate,
    ApiBorrowingStatus Status, string StudentNumber, string StudentName, string EquipmentCode, string EquipmentName);
public sealed record ApiError(string Code, string Message);
public sealed record StudentInput(string? StudentNumber, string? FullName, string? Contact = null);
public sealed record EquipmentInput(string? EquipmentCode, string? Name, string? Description = null);
public sealed record StudentUpdateInput(string? StudentNumber, string? FullName, [property: JsonRequired] string? Contact);
public sealed record EquipmentUpdateInput(string? EquipmentCode, string? Name, [property: JsonRequired] string? Description);
public sealed record EligibilityInput(bool? IsEligible);
public sealed record BorrowingInput(int? StudentId, int? EquipmentId, DateOnly? ExpectedReturnDate);
public sealed record CheckoutInput(int? StudentId, int[]? EquipmentIds, DateOnly? ExpectedReturnDate);
public sealed record CheckoutOutput(IReadOnlyList<ApiBorrowing> Borrowings);
public sealed record ReturnInput;

[JsonConverter(typeof(JsonStringEnumConverter<ApiBorrowingStatus>))]
public enum ApiBorrowingStatus { Active, Returned }

internal static class ApiRevision
{
    public static string Of<T>(T representation) =>
        "\"" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(representation))) + "\"";
}
