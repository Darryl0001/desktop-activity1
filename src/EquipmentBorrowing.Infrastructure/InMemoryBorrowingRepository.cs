namespace EquipmentBorrowing.Infrastructure.Repositories;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class InMemoryBorrowingRepository : IBorrowingRepository
{
    private readonly List<Borrowing> _borrowings = new();

    public Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        _borrowings.Add(borrowing);
        return Task.CompletedTask;
    }

    public Task<int> GetNextIdAsync(CancellationToken cancellationToken = default)
    {
        int nextId = _borrowings.Count > 0 ? _borrowings.Max(b => b.Id) + 1 : 1;
        return Task.FromResult(nextId);
    }
}