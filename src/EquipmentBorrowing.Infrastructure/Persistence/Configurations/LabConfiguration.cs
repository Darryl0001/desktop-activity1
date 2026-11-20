using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public sealed class LabConfiguration : IEntityTypeConfiguration<Student>, IEntityTypeConfiguration<Equipment>, IEntityTypeConfiguration<Borrowing>
{
    // Match .NET string.Trim's Unicode whitespace set, including tabs and nonbreaking spaces.
    private static readonly string WhiteSpace = string.Join(" || ",
        new[] { 9, 10, 11, 12, 13, 32, 133, 160, 5760, 8192, 8193, 8194, 8195, 8196, 8197, 8198, 8199, 8200, 8201, 8202, 8232, 8233, 8239, 8287, 12288 }
            .Select(c => $"chr({c})"));
    private static string Trim(string column) => $"btrim(\"{column}\", {WhiteSpace})";
    public void Configure(EntityTypeBuilder<Student> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).UseIdentityByDefaultColumn();
        b.Property(s => s.Name).IsRequired();
        b.Property(s => s.StudentNumber).IsRequired();
        b.Property<string>("NormalizedNumber").HasComputedColumnSql($"upper({Trim("StudentNumber")})", stored: true);
        b.HasIndex("NormalizedNumber").IsUnique();
        b.Property(s => s.Version).IsRowVersion();
        b.ToTable("Students", t => {
            t.HasCheckConstraint("CK_Student_Required", $"length({Trim("Name")}) > 0 AND length({Trim("StudentNumber")}) > 0");
            t.HasCheckConstraint("CK_Student_Contact", $"\"Contact\" IS NULL OR length({Trim("Contact")}) > 0");
        });
    }
    public void Configure(EntityTypeBuilder<Equipment> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.Name).IsRequired();
        b.Property(e => e.Identifier).IsRequired();
        b.Property<string>("NormalizedIdentifier").HasComputedColumnSql($"upper({Trim("Identifier")})", stored: true);
        b.HasIndex("NormalizedIdentifier").IsUnique();
        b.Property(e => e.Version).IsRowVersion();
        b.ToTable("Equipment", t => {
            t.HasCheckConstraint("CK_Equipment_Required", $"length({Trim("Name")}) > 0 AND length({Trim("Identifier")}) > 0");
            t.HasCheckConstraint("CK_Equipment_Description", $"\"Description\" IS NULL OR length({Trim("Description")}) > 0");
        });
    }
    public void Configure(EntityTypeBuilder<Borrowing> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityByDefaultColumn();
        b.Property(x => x.BorrowedDate).HasColumnType("date");
        b.Property(x => x.ExpectedReturnDate).HasColumnType("date");
        b.Property(x => x.ActualReturnDate).HasColumnType("date");
        b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.EquipmentId).IsUnique().HasFilter("\"Status\" = 0");
        b.HasIndex(x => new { x.StudentId, x.Status });
        b.ToTable("Borrowings", t => {
            t.HasCheckConstraint("CK_Borrowing_Due", "\"ExpectedReturnDate\" > \"BorrowedDate\"");
            t.HasCheckConstraint("CK_Borrowing_Return", "(\"Status\" = 0 AND \"ActualReturnDate\" IS NULL) OR (\"Status\" = 1 AND \"ActualReturnDate\" IS NOT NULL AND \"ActualReturnDate\" >= \"BorrowedDate\")");
        });
    }
}
