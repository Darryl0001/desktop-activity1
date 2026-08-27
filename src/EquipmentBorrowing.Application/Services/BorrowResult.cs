namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Domain;

public class BorrowResult
{
    public bool IsSuccess { get; }
    public string ErrorMessage { get; }
    public Borrowing? Borrowing { get; }

    private BorrowResult(bool isSuccess, string errorMessage, Borrowing? borrowing)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Borrowing = borrowing;
    }

    public static BorrowResult Success(Borrowing borrowing) =>
        new(true, string.Empty, borrowing);

    public static BorrowResult Failure(string errorMessage) =>
        new(false, errorMessage, null);
}