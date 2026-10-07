using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class ServiceTagConfiguration : IEntityTypeConfiguration<ServiceTag>
{
    public void Configure(EntityTypeBuilder<ServiceTag> builder)
    {
        builder.ToTable("ServiceTags");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(60);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}
