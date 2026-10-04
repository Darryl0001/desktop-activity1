using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.IsAvailable)
            .IsRequired();

        builder.HasIndex(e => e.Name);

        builder.HasData(
            new Equipment
            {
                Id = 1,
                Name = "Laptop",
                IsAvailable = true
            },
            new Equipment
            {
                Id = 2,
                Name = "Projector",
                IsAvailable = true
            },
            new Equipment
            {
                Id = 3,
                Name = "Camera",
                IsAvailable = false
            },
            new Equipment
            {
                Id = 4,
                Name = "Microphone",
                IsAvailable = true
            }
        );
    }
}