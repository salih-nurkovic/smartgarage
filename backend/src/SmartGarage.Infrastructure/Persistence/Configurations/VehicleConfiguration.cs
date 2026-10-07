using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", t =>
        {
            t.HasCheckConstraint("CK_Vehicle_ProductionYear",
                $"[ProductionYear] >= {ValidationRules.VehicleYearMin}");
            t.HasCheckConstraint("CK_Vehicle_Mileage",
                $"[CurrentMileageKm] >= 0 AND [CurrentMileageKm] <= {ValidationRules.MileageMaxKm}");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RegistrationPlate)
            .IsRequired()
            .HasMaxLength(ValidationRules.PlateMaxLength);

        builder.Property(x => x.Vin)
            .IsRequired()
            .HasMaxLength(ValidationRules.VinLength)
            .IsFixedLength();

        builder.Property(x => x.ProductionYear).IsRequired();
        builder.Property(x => x.CurrentMileageKm).IsRequired();
        builder.Property(x => x.LastMileageUpdatedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.ModifiedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        // Filtered so an archived vehicle does not block re-registering the same VIN, which
        // happens legitimately when a car changes owner.
        builder.HasIndex(x => x.Vin).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.RegistrationPlate).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.OwnerId);

        // Restrict everywhere: a vehicle with history is archived, never erased, because its
        // appointments carry financial and audit records that must survive.
        builder.HasOne(x => x.Owner)
            .WithMany(x => x.Vehicles)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Brand)
            .WithMany()
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Model)
            .WithMany(x => x.Vehicles)
            .HasForeignKey(x => x.ModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EngineType)
            .WithMany(x => x.Vehicles)
            .HasForeignKey(x => x.EngineTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
