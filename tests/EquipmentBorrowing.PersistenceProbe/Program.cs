using EquipmentBorrowing.Application;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

if (Environment.GetEnvironmentVariable("LAB4_VERIFY_DISPOSABLE") != "1" || args.Length != 1)
{
    Console.Error.WriteLine("Test-only probe requires LAB4_VERIFY_DISPOSABLE=1 and issue|read|return|verify-return.");
    return 2;
}
try
{
    var factory = new ProbeFactory();
    var service = new LabService(new PostgresLabRepository(factory), new ManilaCalendar());
    var phase = args[0];
    if (phase == "issue")
    {
        Check(await service.SaveStudentAsync(new(0, "RESTART-PROBE", "Restart Verification", null)));
        Check(await service.SaveEquipmentAsync(new(0, "RESTART-ITEM-A", "Restart Camera", null)));
        Check(await service.SaveEquipmentAsync(new(0, "RESTART-ITEM-B", "Restart Projector", null)));
    }
    var student = (await service.StudentsAsync()).Single(s => s.StudentNumber == "RESTART-PROBE");
    var items = (await service.EquipmentAsync()).Where(e => e.Identifier.StartsWith("RESTART-ITEM-")).ToArray();
    if (phase == "issue")
    {
        Check(await service.CheckoutAsync(new(student.Id, items.Select(e => e.Id).ToArray(), new ManilaCalendar().Today.AddDays(7), true)));
        Console.WriteLine("PASS: process 1 committed one student, two items, and two active borrowings.");
    }
    else
    {
        var loans = await service.LoansAsync(new(StudentId: student.Id));
        if (loans.Count != 2) throw new InvalidOperationException("Expected two retained loans.");
        if (phase == "read")
        {
            if (loans.Any(b => b.Status != BorrowingStatus.Active) || items.Any(e => e.IsAvailable))
                throw new InvalidOperationException("Active readback mismatch.");
            Console.WriteLine("PASS: new process after server restart read both active borrowings and unavailable items.");
        }
        else if (phase == "return")
        {
            foreach (var loan in loans) Check(await service.ReturnAsync(loan.Id));
            Console.WriteLine("PASS: separate process returned both original borrowing records.");
        }
        else if (phase == "verify-return")
        {
            if (loans.Any(b => b.Status != BorrowingStatus.Returned || b.ActualReturnDate is null) || items.Any(e => !e.IsAvailable))
                throw new InvalidOperationException("Returned readback mismatch.");
            Console.WriteLine("PASS: new process after second server restart read both returned records, actual dates, and available items.");
        }
        else throw new InvalidOperationException("Unknown probe phase.");
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("FAIL: persistence probe " + ex.GetType().Name);
    return 1;
}
static void Check(WriteResult result)
{
    if (!result.IsSuccess) throw new InvalidOperationException(result.Message);
}
sealed class ProbeFactory : IDbContextFactory<EquipmentBorrowingDbContext>
{
    public EquipmentBorrowingDbContext CreateDbContext() => new(new DbContextOptionsBuilder<EquipmentBorrowingDbContext>()
        .UseNpgsql(DatabaseConfiguration.ConnectionString).Options);
}
