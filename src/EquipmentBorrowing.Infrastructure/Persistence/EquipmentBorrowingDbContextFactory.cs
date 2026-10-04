using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace EquipmentBorrowing.Infrastructure.Persistence;

public class EquipmentBorrowingDbContextFactory
    : IDesignTimeDbContextFactory<EquipmentBorrowingDbContext>
{
    public EquipmentBorrowingDbContext CreateDbContext(string[] args)
    {
        var projectDirectory = Directory.GetCurrentDirectory();

        var databasePath = Path.GetFullPath(
            Path.Combine(
                projectDirectory,
                "..",
                "..",
                "Data",
                "equipment-borrowing.db"));

        var optionsBuilder =
            new DbContextOptionsBuilder<EquipmentBorrowingDbContext>();

        optionsBuilder.UseSqlite($"Data Source={databasePath}");

        return new EquipmentBorrowingDbContext(optionsBuilder.Options);
    }
}