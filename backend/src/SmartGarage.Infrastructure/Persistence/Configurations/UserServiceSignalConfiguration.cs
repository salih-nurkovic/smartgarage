using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class UserServiceSignalConfiguration : IEntityTypeConfiguration<UserServiceSignal>
{
    public void Configure(EntityTypeBuilder<UserServiceSignal> builder)
    {
        builder.ToTable("UserServiceSignals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SignalType).IsRequired().HasConversion<int>();
        builder.Property(x => x.Weight).IsRequired();
        builder.Property(x => x.OccurredAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.PerformedAtUtc).HasColumnType("datetime2");

        // The recommender aggregates signals per user with a GroupBy in SQL. These indexes are
        // what keep that to a single query instead of one per service.
        builder.HasIndex(x => new { x.UserId, x.OccurredAtUtc }).IsDescending(false, true);
        builder.HasIndex(x => new { x.UserId, x.ServiceId });
        builder.HasIndex(x => new { x.VehicleId, x.SignalType });

        builder.HasOne(x => x.User)
            .WithMany(x => x.ServiceSignals)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Service)
            .WithMany(x => x.Signals)
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vehicle)
            .WithMany(x => x.Signals)
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
