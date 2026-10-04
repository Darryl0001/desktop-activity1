using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure;

public class EfStudentRepository : IStudentRepository
{
    private readonly EquipmentBorrowingDbContext _dbContext;

    public EfStudentRepository(EquipmentBorrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Student?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == id,
                cancellationToken);
    }

    public async Task UpdateAsync(
        Student student,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Students.Update(student);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}