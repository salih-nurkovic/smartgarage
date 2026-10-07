using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.ToTable("ErrorLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Level).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.ExceptionType).HasMaxLength(300);

        // Unbounded on purpose: a truncated stack trace is often a useless one, and this
        // column is the whole point of the table.
        builder.Property(x => x.StackTrace).HasColumnType("nvarchar(max)");

        builder.Property(x => x.RequestPath).HasMaxLength(500);
        builder.Property(x => x.HttpMethod).HasMaxLength(10);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(20);
        builder.Property(x => x.OccurredAtUtc).IsRequired().HasColumnType("datetime2");

        builder.HasIndex(x => x.OccurredAtUtc).IsDescending();
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => new { x.Level, x.OccurredAtUtc });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
