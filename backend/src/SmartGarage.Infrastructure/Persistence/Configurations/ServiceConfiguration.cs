using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services", t =>
        {
            t.HasCheckConstraint("CK_Service_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_Service_Duration",
                $"[EstimatedDurationMinutes] >= {ValidationRules.DurationMinMinutes} "
                + $"AND [EstimatedDurationMinutes] <= {ValidationRules.DurationMaxMinutes}");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ImagePath).HasMaxLength(300);

        // Money is decimal(18,2). Never float, never double.
        builder.Property(x => x.Price).IsRequired().HasColumnType("decimal(18,2)");

        builder.Property(x => x.EstimatedDurationMinutes).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.ModifiedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.CategoryId, x.IsActive });
        builder.HasIndex(x => x.Price);
    }
}
