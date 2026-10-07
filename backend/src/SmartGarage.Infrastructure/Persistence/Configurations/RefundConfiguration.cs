using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", t =>
            t.HasCheckConstraint("CK_Refund_Amount", "[Amount] > 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(ValidationRules.ReasonMaxLength);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(RefundStatus.Pending);

        builder.Property(x => x.StripeRefundId).IsRequired().HasMaxLength(120);
        builder.Property(x => x.RequestedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => x.StripeRefundId).IsUnique();
        builder.HasIndex(x => x.PaymentId);

        builder.HasOne(x => x.Payment)
            .WithMany(x => x.Refunds)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
