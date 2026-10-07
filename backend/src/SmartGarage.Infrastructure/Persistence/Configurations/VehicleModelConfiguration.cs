using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class VehicleModelConfiguration : IEntityTypeConfiguration<VehicleModel>
{
    public void Configure(EntityTypeBuilder<VehicleModel> builder)
    {
        builder.ToTable("VehicleModels");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(80);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        // The same model name may exist under two different brands, but not twice under one.
        builder.HasIndex(x => new { x.BrandId, x.Name }).IsUnique();
    }
}
