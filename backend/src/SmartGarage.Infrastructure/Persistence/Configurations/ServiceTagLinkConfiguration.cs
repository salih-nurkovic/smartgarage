using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class ServiceTagLinkConfiguration : IEntityTypeConfiguration<ServiceTagLink>
{
    public void Configure(EntityTypeBuilder<ServiceTagLink> builder)
    {
        builder.ToTable("ServiceTagLinks", t =>
            t.HasCheckConstraint("CK_ServiceTagLink_Weight", "[Weight] > 0 AND [Weight] <= 1"));

        // Composite key: a service carries a given tag once.
        builder.HasKey(x => new { x.ServiceId, x.ServiceTagId });

        builder.Property(x => x.Weight).IsRequired().HasDefaultValue(1.0);

        builder.HasOne(x => x.Service)
            .WithMany(x => x.TagLinks)
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a tag in use cannot be deleted from the reference list.
        builder.HasOne(x => x.ServiceTag)
            .WithMany(x => x.ServiceLinks)
            .HasForeignKey(x => x.ServiceTagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
