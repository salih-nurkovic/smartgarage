using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.ToTable("ServiceCategories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.IconPath).HasMaxLength(300);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        // Unique only among live rows: a name freed by archiving a category can be reused.
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.DisplayOrder);

        builder.HasMany(x => x.Services)
            .WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
