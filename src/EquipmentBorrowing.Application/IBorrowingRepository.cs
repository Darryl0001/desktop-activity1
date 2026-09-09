namespace EquipmentBorrowing.Application.Interfaces;

using EquipmentBorrowing.Domain;

public interface IBorrowingRepository
{
    Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default);
    Task<int> GetNextIdAsync(CancellationToken cancellationToken = default);
    Task<Borrowing?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Borrowing>> GetActiveBorrowingsAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default);
}