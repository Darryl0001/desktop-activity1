using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Persistence;

public static class DatabaseSetup
{
    public static async Task SeedAsync(EquipmentBorrowingDbContext db, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(814004)", ct);
        if (!await db.Students.AnyAsync(ct) && !await db.Equipment.AnyAsync(ct))
        {
            db.Students.AddRange(
                new Student { Name = "Juan Dela Cruz", StudentNumber = "2026-0001" },
                new Student { Name = "Maria Santos", StudentNumber = "2026-0002" },
                new Student { Name = "Pedro Reyes", StudentNumber = "2026-0003" });
            db.Equipment.AddRange(new[] { "Laptop", "Projector", "Camera", "Microphone", "Keyboard", "Tripod" }
                .Select((name, i) => new Equipment { Name = name, Identifier = $"EQ-{i + 1:000}" }));
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
