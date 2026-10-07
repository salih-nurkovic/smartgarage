using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", t =>
            t.HasCheckConstraint("CK_Review_Rating",
                $"[Rating] >= {ValidationRules.RatingMin} AND [Rating] <= {ValidationRules.RatingMax}"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Rating).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.IsVisible).IsRequired().HasDefaultValue(true);

        // One review per appointment, enforced in the schema rather than only in a service.
        builder.HasIndex(x => x.AppointmentId).IsUnique();
        builder.HasIndex(x => new { x.ServiceId, x.IsVisible });
        builder.HasIndex(x => x.AssignedEmployeeId);

        builder.HasOne(x => x.Appointment)
            .WithOne(x => x.Review)
            .HasForeignKey<Review>(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Service)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Author)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.AssignedEmployee)
            .WithMany()
            .HasForeignKey(x => x.AssignedEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
