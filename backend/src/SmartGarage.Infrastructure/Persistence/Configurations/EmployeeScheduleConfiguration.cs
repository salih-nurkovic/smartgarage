using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class EmployeeScheduleConfiguration : IEntityTypeConfiguration<EmployeeSchedule>
{
    public void Configure(EntityTypeBuilder<EmployeeSchedule> builder)
    {
        builder.ToTable("EmployeeSchedules", t =>
            // An employee cannot finish before they start. Enforced in the database so no code
            // path can write a schedule that would make slot calculation nonsensical.
            t.HasCheckConstraint("CK_EmployeeSchedule_EndAfterStart", "[EndTime] > [StartTime]"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek).IsRequired().HasConversion<int>();
        builder.Property(x => x.StartTime).IsRequired().HasColumnType("time");
        builder.Property(x => x.EndTime).IsRequired().HasColumnType("time");
        builder.Property(x => x.IsWorkingDay).IsRequired().HasDefaultValue(true);

        // One row per employee per weekday.
        builder.HasIndex(x => new { x.EmployeeProfileId, x.DayOfWeek }).IsUnique();
    }
}
