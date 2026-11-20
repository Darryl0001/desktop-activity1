using EquipmentBorrowing.Application;

namespace EquipmentBorrowing.Infrastructure.Persistence;

public static class DatabaseConfiguration
{
    public static string ConnectionString => Environment.GetEnvironmentVariable("LAB4_CONNECTION_STRING")
        ?? "Host=localhost;Database=equipment_borrowing;Username=postgres;Timeout=5;Command Timeout=15";
}
public sealed class ManilaCalendar : ICalendar
{
    public DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")).Date;
}
