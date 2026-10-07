using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class EngineTypeConfiguration : IEntityTypeConfiguration<EngineType>
{
    public void Configure(EntityTypeBuilder<EngineType> builder)
    {
        builder.ToTable("EngineTypes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
