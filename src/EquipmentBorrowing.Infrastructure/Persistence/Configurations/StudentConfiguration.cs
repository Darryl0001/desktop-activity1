using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.IsAllowedToBorrow)
            .IsRequired();

        builder.Property(s => s.ActiveBorrowingsCount)
            .IsRequired();

        builder.Property(s => s.MaxAllowedBorrowings)
            .IsRequired();


        builder.HasData(
            new Student
            {
                Id = 1,
                Name = "Juan Dela Cruz",
                IsAllowedToBorrow = true,
                ActiveBorrowingsCount = 0,
                MaxAllowedBorrowings = 3
            },
            new Student
            {
                Id = 2,
                Name = "Maria Santos",
                IsAllowedToBorrow = false,
                ActiveBorrowingsCount = 0,
                MaxAllowedBorrowings = 3
            },
            new Student
            {
                Id = 3,
                Name = "Pedro Reyes",
                IsAllowedToBorrow = true,
                ActiveBorrowingsCount = 3,
                MaxAllowedBorrowings = 3
            }
        );
    }
}