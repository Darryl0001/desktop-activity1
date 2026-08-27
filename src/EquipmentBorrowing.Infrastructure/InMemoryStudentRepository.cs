namespace EquipmentBorrowing.Infrastructure.Repositories;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class InMemoryStudentRepository : IStudentRepository
{
    private readonly List<Student> _students = new()
    {
        new Student { Id = 1, Name = "Darryl Macs", IsAllowedToBorrow = true, ActiveBorrowingsCount = 0, MaxAllowedBorrowings = 3 },
        new Student { Id = 2, Name = "Brent Marcus", IsAllowedToBorrow = false, ActiveBorrowingsCount = 0, MaxAllowedBorrowings = 3 },
        new Student { Id = 3, Name = "Gil Scudge", IsAllowedToBorrow = true, ActiveBorrowingsCount = 3, MaxAllowedBorrowings = 3 }
    };

    public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = _students.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(student);
    }

    public Task UpdateAsync(Student student, CancellationToken cancellationToken = default)
    {
        var existingIndex = _students.FindIndex(s => s.Id == student.Id);
        if (existingIndex != -1)
        {
            _students[existingIndex] = student;
        }
        return Task.CompletedTask;
    }
}