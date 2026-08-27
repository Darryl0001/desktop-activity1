using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Infrastructure.Repositories;

Console.WriteLine("=============================================");
Console.WriteLine(" CAMPUS EQUIPMENT BORROWING SYSTEM DEMO ");
Console.WriteLine("=============================================\n");

var studentRepo = new InMemoryStudentRepository();
var equipmentRepo = new InMemoryEquipmentRepository();
var borrowingRepo = new InMemoryBorrowingRepository();

var borrowingService = new BorrowEquipmentService(studentRepo, equipmentRepo, borrowingRepo);

// scenario 1
Console.WriteLine("Scneario 1 - SUCCESS");
Console.WriteLine("Darryl Macs - borrows keyboard (ID: 101)...");

var successResult = await borrowingService.ExecuteAsync(studentId: 1, equipmentId: 101);

if (successResult.IsSuccess && successResult.Borrowing != null)
{
    Console.WriteLine("Result: SUCCESS!");
    Console.WriteLine($"  - Transaction ID: {successResult.Borrowing.Id}");
    Console.WriteLine($"  - Student ID:     {successResult.Borrowing.StudentId}");
    Console.WriteLine($"  - Equipment ID:   {successResult.Borrowing.EquipmentId}");
    Console.WriteLine($"  - Borrow Date:    {successResult.Borrowing.BorrowedDate}");
    Console.WriteLine($"  - Due Date:       {successResult.Borrowing.ExpectedReturnDate}");
    Console.WriteLine($"  - Status:         {successResult.Borrowing.Status}");
}
else
{
    Console.WriteLine($"Result: FAILED - {successResult.ErrorMessage}");
}

Console.WriteLine();

// scenario 2
Console.WriteLine("Scenario 2: FAILURE (unavailable)");
Console.WriteLine("Attempting: Darryl Macs borrows mouse");

var failedItemResult = await borrowingService.ExecuteAsync(studentId: 1, equipmentId: 102);

if (failedItemResult.IsSuccess)
{
    Console.WriteLine("Result: SUCCESS!");
}
else
{
    Console.WriteLine($"Result: FAILED (As Expected)");
    Console.WriteLine($"  - Reason: {failedItemResult.ErrorMessage}");
}

Console.WriteLine();

// scenario 3
Console.WriteLine("Scenario 3: FAILURE (Student Disallowed)");
Console.WriteLine("Attempting: Brent Marcus borrows Projector");

var failedStudentResult = await borrowingService.ExecuteAsync(studentId: 2, equipmentId: 103);

if (failedStudentResult.IsSuccess)
{
    Console.WriteLine("Result: SUCCESS!");
}
else
{
    Console.WriteLine($"Result: FAILED (As Expected)");
    Console.WriteLine($"  - Reason: {failedStudentResult.ErrorMessage}");
}

Console.WriteLine("\n=============================================");
Console.WriteLine(" DEMO COMPLETED ");
Console.WriteLine("=============================================");