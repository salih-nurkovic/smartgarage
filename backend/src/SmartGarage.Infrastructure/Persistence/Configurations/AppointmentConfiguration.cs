using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments", t =>
        {
            t.HasCheckConstraint("CK_Appointment_EndAfterStart",
                "[ScheduledEndUtc] > [ScheduledStartUtc]");
            t.HasCheckConstraint("CK_Appointment_TotalPrice", "[TotalPrice] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AppointmentNumber).IsRequired().HasMaxLength(24);
        builder.Property(x => x.ScheduledStartUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.ScheduledEndUtc).IsRequired().HasColumnType("datetime2");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(AppointmentStatus.Pending);

        builder.Property(x => x.PaymentStatus)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(AppointmentPaymentStatus.Unpaid);

        builder.Property(x => x.TotalPrice).IsRequired().HasColumnType("decimal(18,2)");

        builder.Property(x => x.CustomerNotes).HasMaxLength(1000);
        builder.Property(x => x.InternalNotes).HasMaxLength(2000);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);

        builder.Property(x => x.CancelledAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.ConfirmedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.ModifiedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        // The number is what users see, so it must be unique and fast to look up.
        builder.HasIndex(x => x.AppointmentNumber).IsUnique();

        // These three carry the conflict check, the customer's history screen and the
        // desktop appointment list respectively.
        builder.HasIndex(x => new { x.AssignedEmployeeId, x.ScheduledStartUtc });
        builder.HasIndex(x => new { x.CustomerId, x.ScheduledStartUtc });
        builder.HasIndex(x => new { x.Status, x.ScheduledStartUtc });
        builder.HasIndex(x => new { x.VehicleId, x.ScheduledStartUtc });

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vehicle)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull: removing an employee from the roster must not take the appointment with it.
        builder.HasOne(x => x.AssignedEmployee)
            .WithMany(x => x.AssignedAppointments)
            .HasForeignKey(x => x.AssignedEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        // Audit foreign keys use NoAction. SQL Server refuses a table with several cascade
        // paths back to the same parent, and these four all point at AspNetUsers.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
