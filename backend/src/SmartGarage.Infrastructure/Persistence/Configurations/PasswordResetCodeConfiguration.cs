using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class PasswordResetCodeConfiguration : IEntityTypeConfiguration<PasswordResetCode>
{
    public void Configure(EntityTypeBuilder<PasswordResetCode> builder)
    {
        builder.ToTable("PasswordResetCodes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CodeHash).IsRequired().HasMaxLength(200);
        builder.Property(x => x.FailedAttempts).IsRequired().HasDefaultValue(0);

        builder.Property(x => x.ExpiresAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.UsedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");

        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}