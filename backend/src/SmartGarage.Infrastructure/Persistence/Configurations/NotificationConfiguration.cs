using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired().HasConversion<int>();
        builder.Property(x => x.Title).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.IsRead).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(60);

        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.ReadAtUtc).HasColumnType("datetime2");

        // Serves both the inbox list and the unread badge count in one index.
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc })
            .IsDescending(false, false, true);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
