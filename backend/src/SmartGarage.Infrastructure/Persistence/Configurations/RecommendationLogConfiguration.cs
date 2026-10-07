using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class RecommendationLogConfiguration : IEntityTypeConfiguration<RecommendationLog>
{
    public void Configure(EntityTypeBuilder<RecommendationLog> builder)
    {
        builder.ToTable("RecommendationLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Score).IsRequired();
        builder.Property(x => x.Rank).IsRequired();
        builder.Property(x => x.Urgency).IsRequired().HasConversion<int>();
        builder.Property(x => x.ExplanationText).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ExplanationFactorsJson).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.AlgorithmVersion).IsRequired().HasMaxLength(40);
        builder.Property(x => x.GeneratedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.WasClicked).IsRequired().HasDefaultValue(false);

        builder.HasIndex(x => new { x.UserId, x.VehicleId, x.GeneratedAtUtc })
            .IsDescending(false, false, true);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Vehicle)
            .WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
