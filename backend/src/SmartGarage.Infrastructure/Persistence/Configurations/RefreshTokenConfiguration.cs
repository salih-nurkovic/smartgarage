using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(100);
        builder.Property(x => x.JwtId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RevokedReason).HasMaxLength(200);
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);

        builder.Property(x => x.ExpiresAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.RevokedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");

        builder.HasIndex(x => x.TokenHash).IsUnique();

        // Logout looks sessions up by jti to deny the matching access token.
        builder.HasIndex(x => x.JwtId);
        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });

        builder.Ignore(x => x.IsActive);

        builder.HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
