using EquipmentBorrowing.Application;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure;

public class EfBorrowingRepository : IBorrowingRepository
{
    private readonly EquipmentBorrowingDbContext _dbContext;

    public EfBorrowingRepository(EquipmentBorrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Borrowing borrowing,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Borrowings.AddAsync(
            borrowing,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetNextIdAsync(
        CancellationToken cancellationToken = default)
    {
        var maxId = await _dbContext.Borrowings
            .AsNoTracking()
            .Select(b => (int?)b.Id)
            .MaxAsync(cancellationToken);

        return (maxId ?? 0) + 1;
    }

    public async Task<Borrowing?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Borrowings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.Id == id,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Borrowing>> GetActiveBorrowingsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Borrowings
            .AsNoTracking()
            .Where(b => b.Status == BorrowingStatus.Active)
            .OrderByDescending(b => b.BorrowedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Borrowing borrowing,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Borrowings.Update(borrowing);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActiveBorrowingDetails>> GetActiveBorrowingsWithDetailsAsync(
        CancellationToken cancellationToken = default)
    {
        return await (
            from borrowing in _dbContext.Borrowings.AsNoTracking()
            join student in _dbContext.Students
                on borrowing.StudentId equals student.Id
            join equipment in _dbContext.Equipment
                on borrowing.EquipmentId equals equipment.Id
            where borrowing.Status == BorrowingStatus.Active
            orderby borrowing.BorrowedDate descending
            select new ActiveBorrowingDetails(
                borrowing.Id,
                student.Name,
                equipment.Name,
                borrowing.BorrowedDate,
                borrowing.ExpectedReturnDate)
        ).ToListAsync(cancellationToken);
    }


    public async Task<IReadOnlyList<Borrowing>> GetOverdueBorrowingsAsync(
    CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _dbContext.Borrowings
            .AsNoTracking()
            .Where(b =>
                b.Status == BorrowingStatus.Active &&
                b.ExpectedReturnDate < now)
            .OrderBy(b => b.ExpectedReturnDate)
            .ToListAsync(cancellationToken);
    }

    
}

