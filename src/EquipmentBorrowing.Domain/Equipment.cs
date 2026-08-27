namespace EquipmentBorrowing.Domain;

public class Equipment
{
    public int Id {get; set; }
    public string Name { get; set; } = "";
    public bool IsAvailable { get; set;} = true;
}