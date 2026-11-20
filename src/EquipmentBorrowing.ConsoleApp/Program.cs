using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

if (args.Length != 1 || args[0] is not ("migrate" or "seed"))
{
    Console.WriteLine("Database setup: dotnet run --project src/EquipmentBorrowing.ConsoleApp -- migrate|seed");
    return 1;
}
try
{
    await using var db = new EquipmentBorrowingDbContextFactory().CreateDbContext(args);
    if (args[0] == "migrate") await db.Database.MigrateAsync();
    else await DatabaseSetup.SeedAsync(db);
    Console.WriteLine(args[0] == "migrate" ? "PostgreSQL migrations applied." : "Safe initial data checked. Existing data preserved.");
    return 0;
}
catch
{
    Console.Error.WriteLine("Database setup failed. Check PostgreSQL, LAB4_CONNECTION_STRING, database permissions, and migration prerequisites. Connection details are not logged.");
    return 1;
}
