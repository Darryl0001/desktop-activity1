namespace EquipmentBorrowing.Domain;

public class Student 
{
    public int Id { get; set; }
    public string Name { get; set;  }
    public bool IsAllowedToBorrow { get; set; } = true;
    public int ActiveBorrowingsCount { get; set;} 
    public int MaxAllowedBorrowings { get; set; } = 3;

    public bool CanBorrow()
    {
        return IsAllowedToBorrow && ActiveBorrowingsCount < MaxAllowedBorrowings;
    }
}