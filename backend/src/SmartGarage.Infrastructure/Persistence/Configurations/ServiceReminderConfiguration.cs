using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class ServiceReminderConfiguration : IEntityTypeConfiguration<ServiceReminder>
{
    public void Configure(EntityTypeBuilder<ServiceReminder> builder)
    {
        builder.ToTable("ServiceReminders", t =>
            // A reminder that is due neither by date nor by mileage would never fire.
            t.HasCheckConstraint("CK_ServiceReminder_HasDueCondition",
                "[DueDateUtc] IS NOT NULL OR [DueMileageKm] IS NOT NULL"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ServiceReminderStatus.Pending);

        builder.Property(x => x.DueDateUtc).HasColumnType("datetime2");
        builder.Property(x => x.SentAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");

        // The Worker's nightly scan filters on exactly this pair.
        builder.HasIndex(x => new { x.Status, x.DueDateUtc });
        builder.HasIndex(x => new { x.VehicleId, x.Status });

        builder.HasOne(x => x.Vehicle)
            .WithMany(x => x.Reminders)
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Service)
            .WithMany(x => x.Reminders)
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GeneratedFromAppointment)
            .WithMany()
            .HasForeignKey(x => x.GeneratedFromAppointmentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
