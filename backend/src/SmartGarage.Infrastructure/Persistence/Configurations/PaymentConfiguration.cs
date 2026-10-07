using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
            t.HasCheckConstraint("CK_Payment_Amount", "[Amount] > 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PaymentNumber).IsRequired().HasMaxLength(24);
        builder.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(x => x.AmountChargedStripe).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.StripeCurrency).IsRequired().HasMaxLength(3).IsFixedLength();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(PaymentStatus.Created);

        builder.Property(x => x.StripePaymentIntentId).IsRequired().HasMaxLength(120);
        builder.Property(x => x.StripeChargeId).HasMaxLength(120);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(120);

        builder.Property(x => x.PaidAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("datetime2");

        builder.HasIndex(x => x.PaymentNumber).IsUnique();

        // Two independent guards against double payment: one PaymentIntent can produce only
        // one payment row, and one idempotency key can be used only once.
        builder.HasIndex(x => x.StripePaymentIntentId).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        builder.HasIndex(x => new { x.AppointmentId, x.Status });
        builder.HasIndex(x => new { x.PayerId, x.CreatedAtUtc });

        // Restrict on both: financial records never disappear because something upstream was
        // removed.
        builder.HasOne(x => x.Appointment)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Payer)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.PayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
