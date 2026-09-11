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
    public Task<Borrowing?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var borrowing = _borrowings.FirstOrDefault(b => b.Id == id);
        return Task.FromResult(borrowing);
    }

    public Task<IReadOnlyList<Borrowing>> GetActiveBorrowingsAsync(CancellationToken cancellationToken = default)
    {
        var active = _borrowings.Where(b => b.Status == BorrowingStatus.Active).ToList();
        return Task.FromResult<IReadOnlyList<Borrowing>>(active);
    }

    public Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        var existingIndex = _borrowings.FindIndex(b => b.Id == borrowing.Id);
        if (existingIndex != -1)
        {
            _borrowings[existingIndex] = borrowing;
        }
        return Task.CompletedTask;
    }
}