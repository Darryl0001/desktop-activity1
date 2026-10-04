using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EquipmentBorrowing.Infrastructure.Persistence;

public class EquipmentBorrowingDbContextFactory
    : IDesignTimeDbContextFactory<EquipmentBorrowingDbContext>
{
    public EquipmentBorrowingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<EquipmentBorrowingDbContext>();

        optionsBuilder.UseSqlite(
            "Data Source=equipment-borrowing.db");

        return new EquipmentBorrowingDbContext(optionsBuilder.Options);
    }
}