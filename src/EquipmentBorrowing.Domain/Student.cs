namespace EquipmentBorrowing.Domain;

public class Student 
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string StudentNumber { get; set; } = "";
    public string? Contact { get; set; }
    public uint Version { get; set; }
    public bool IsAllowedToBorrow { get; set; } = true;
}
