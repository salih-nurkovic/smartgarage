using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class AppointmentStatusHistoryConfiguration : IEntityTypeConfiguration<AppointmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentStatusHistory> builder)
    {
        builder.ToTable("AppointmentStatusHistories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FromStatus).HasConversion<int>();
        builder.Property(x => x.ToStatus).IsRequired().HasConversion<int>();
        builder.Property(x => x.ChangedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.ActorRole).IsRequired().HasMaxLength(40);

        builder.HasIndex(x => new { x.AppointmentId, x.ChangedAtUtc });

        builder.HasOne(x => x.Appointment)
            .WithMany(x => x.History)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
