namespace EquipmentBorrowing.Domain;

public class Equipment
{
    public int Id {get; set; }
    public string Name { get; set; } = "";
    public string Identifier { get; set; } = "";
    public string? Description { get; set; }
    public uint Version { get; set; }
    public bool IsAvailable { get; set;} = true;
}
