namespace EquipmentBorrowing.Application;

public record ActiveBorrowingDetails(
    int BorrowingId,
    string StudentName,
    string EquipmentName,
    DateTime BorrowedDate,
    DateTime ExpectedReturnDate);