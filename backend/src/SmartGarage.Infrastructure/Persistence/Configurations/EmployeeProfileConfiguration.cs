using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeProfile> builder)
    {
        builder.ToTable("EmployeeProfiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Position).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Specialization).HasMaxLength(120);
        builder.Property(x => x.HireDateUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.IsAvailableForAssignment).IsRequired().HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.ModifiedAtUtc).HasColumnType("datetime2");

        // One employment record per person.
        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasMany(x => x.Schedules)
            .WithOne(x => x.EmployeeProfile)
            .HasForeignKey(x => x.EmployeeProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
