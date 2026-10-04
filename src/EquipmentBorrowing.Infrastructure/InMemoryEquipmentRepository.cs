namespace EquipmentBorrowing.Infrastructure.Repositories;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly List<Equipment> _equipments = new()
    {
        new Equipment { Id = 101, Name = "Keyboards", IsAvailable = true },
        new Equipment { Id = 102, Name = "Mouse", IsAvailable = false },
        new Equipment { Id = 103, Name = "Projector", IsAvailable = true }
    };

    public Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var equipment = _equipments.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(equipment);
    }

    public Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        var existingIndex = _equipments.FindIndex(e => e.Id == equipment.Id);
        if (existingIndex != -1)
        {
            _equipments[existingIndex] = equipment;
        }
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Equipment>>(_equipments.ToList());
    }

    public Task<IReadOnlyList<Equipment>> GetAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        var equipment = _equipments
            .Where(e => e.IsAvailable)
            .OrderBy(e => e.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<Equipment>>(equipment);
    }
}