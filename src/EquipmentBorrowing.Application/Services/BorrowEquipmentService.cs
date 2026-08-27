namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class BorrowEquipmentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IBorrowingRepository _borrowingRepository;

    public BorrowEquipmentService(
        IStudentRepository studentRepository,
        IEquipmentRepository equipmentRepository,
        IBorrowingRepository borrowingRepository)
    {
        _studentRepository = studentRepository;
        _equipmentRepository = equipmentRepository;
        _borrowingRepository = borrowingRepository;
    }

    public async Task<BorrowResult> ExecuteAsync(
        int studentId,
        int equipmentId,
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetByIdAsync(studentId, cancellationToken);
        if (student == null)
        {
            return BorrowResult.Failure("Student not found.");
        }

        if (!student.IsAllowedToBorrow)
        {
            return BorrowResult.Failure($"Student '{student.Name}' is currently not allowed to borrow equipment.");
        }

        if (student.ActiveBorrowingsCount >= student.MaxAllowedBorrowings)
        {
            return BorrowResult.Failure($"Student '{student.Name}' has reached the maximum allowed borrowings limit of {student.MaxAllowedBorrowings}.");
        }

        var equipment = await _equipmentRepository.GetByIdAsync(equipmentId, cancellationToken);
        if (equipment == null)
        {
            return BorrowResult.Failure("Equipment not found.");
        }

        if (!equipment.IsAvailable)
        {
            return BorrowResult.Failure($"Equipment '{equipment.Name}' is currently unavailable.");
        }

        int nextBorrowingId = await _borrowingRepository.GetNextIdAsync(cancellationToken);
        var borrowing = new Borrowing(nextBorrowingId, student.Id, equipment.Id);

        // Update states
        equipment.IsAvailable = false;
        student.ActiveBorrowingsCount++;

        // Persist updates
        await _equipmentRepository.UpdateAsync(equipment, cancellationToken);
        await _studentRepository.UpdateAsync(student, cancellationToken);
        await _borrowingRepository.AddAsync(borrowing, cancellationToken);

        return BorrowResult.Success(borrowing);
    }
}