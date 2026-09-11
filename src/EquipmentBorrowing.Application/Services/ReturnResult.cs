namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Domain;

public class ReturnResult
{
    public bool IsSuccess { get; }
    public Borrowing? Borrowing { get; }
    public string? ErrorMessage { get; }

    private ReturnResult(bool isSuccess, Borrowing? borrowing, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Borrowing = borrowing;
        ErrorMessage = errorMessage;
    }

    public static ReturnResult Success(Borrowing borrowing) => new(true, borrowing, null);
    public static ReturnResult Failure(string errorMessage) => new(false, null, errorMessage);
}