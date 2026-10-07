using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class VehicleBrandConfiguration : IEntityTypeConfiguration<VehicleBrand>
{
    public void Configure(EntityTypeBuilder<VehicleBrand> builder)
    {
        builder.ToTable("VehicleBrands");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(60);
        builder.Property(x => x.LogoPath).HasMaxLength(300);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name).IsUnique();

        // Restrict: a brand cannot disappear while models reference it. The UI disables the
        // delete button and explains why, rather than letting the database refuse at runtime.
        builder.HasMany(x => x.Models)
            .WithOne(x => x.Brand)
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
