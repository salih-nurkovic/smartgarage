using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class ServiceEngineTypeLinkConfiguration : IEntityTypeConfiguration<ServiceEngineTypeLink>
{
    public void Configure(EntityTypeBuilder<ServiceEngineTypeLink> builder)
    {
        builder.ToTable("ServiceEngineTypeLinks");
        builder.HasKey(x => new { x.ServiceId, x.EngineTypeId });

        builder.HasOne(x => x.Service)
            .WithMany(x => x.EngineTypeLinks)
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.EngineType)
            .WithMany(x => x.ServiceLinks)
            .HasForeignKey(x => x.EngineTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
