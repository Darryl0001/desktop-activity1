using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EquipmentBorrowing.Infrastructure.Persistence;

public class EquipmentBorrowingDbContextFactory : IDesignTimeDbContextFactory<EquipmentBorrowingDbContext>
{
    public EquipmentBorrowingDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<EquipmentBorrowingDbContext>().UseNpgsql(DatabaseConfiguration.ConnectionString).Options);
}
