namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class ReturnEquipmentService
{
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IStudentRepository _studentRepository;

    public ReturnEquipmentService(
        IBorrowingRepository borrowingRepository,
        IEquipmentRepository equipmentRepository,
        IStudentRepository studentRepository)
    {
        _borrowingRepository = borrowingRepository;
        _equipmentRepository = equipmentRepository;
        _studentRepository = studentRepository;
    }

    public async Task<ReturnResult> ExecuteAsync(int borrowingId, CancellationToken cancellationToken = default)
    {
        var borrowing = await _borrowingRepository.GetByIdAsync(borrowingId, cancellationToken);
        if (borrowing == null)
        {
            return ReturnResult.Failure("Borrowing record not found.");
        }

        if (borrowing.Status == BorrowingStatus.Returned)
        {
            return ReturnResult.Failure("This borrowing has already been returned.");
        }

        borrowing.Status = BorrowingStatus.Returned;

        var equipment = await _equipmentRepository.GetByIdAsync(borrowing.EquipmentId, cancellationToken);
        if (equipment != null)
        {
            equipment.IsAvailable = true;
            await _equipmentRepository.UpdateAsync(equipment, cancellationToken);
        }

        var student = await _studentRepository.GetByIdAsync(borrowing.StudentId, cancellationToken);
        if (student != null)
        {
            student.ActiveBorrowingsCount = Math.Max(0, student.ActiveBorrowingsCount - 1);
            await _studentRepository.UpdateAsync(student, cancellationToken);
        }

        await _borrowingRepository.UpdateAsync(borrowing, cancellationToken);

        return ReturnResult.Success(borrowing);
    }
}