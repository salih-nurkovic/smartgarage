using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class AppointmentItemConfiguration : IEntityTypeConfiguration<AppointmentItem>
{
    public void Configure(EntityTypeBuilder<AppointmentItem> builder)
    {
        builder.ToTable("AppointmentItems", t =>
        {
            t.HasCheckConstraint("CK_AppointmentItem_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_AppointmentItem_UnitPrice", "[UnitPriceSnapshot] >= 0");
            t.HasCheckConstraint("CK_AppointmentItem_LineTotal", "[LineTotal] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UnitPriceSnapshot).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(x => x.LineTotal).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(x => x.DurationMinutesSnapshot).IsRequired();
        builder.Property(x => x.Quantity).IsRequired().HasDefaultValue(1);

        // A service appears at most once per appointment; quantity carries repetition.
        builder.HasIndex(x => new { x.AppointmentId, x.ServiceId }).IsUnique();

        builder.HasOne(x => x.Appointment)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a service that appears in history can never be hard-deleted. The price
        // snapshot on this row keeps old totals correct regardless.
        builder.HasOne(x => x.Service)
            .WithMany(x => x.AppointmentItems)
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
