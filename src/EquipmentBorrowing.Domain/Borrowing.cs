namespace EquipmentBorrowing.Domain;

public class Borrowing
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int EquipmentId { get; set; }
    public DateTime BorrowedDate { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
    public BorrowingStatus Status { get; set; }

    public Borrowing() { }

    public Borrowing(int id, int studentId, int equipmentId, int durationDays = 7)
    {
        Id = id;
        StudentId = studentId;
        EquipmentId = equipmentId;
        BorrowedDate = DateTime.UtcNow;
        ExpectedReturnDate = BorrowedDate.AddDays(durationDays);
        Status = BorrowingStatus.Active;
    }
}