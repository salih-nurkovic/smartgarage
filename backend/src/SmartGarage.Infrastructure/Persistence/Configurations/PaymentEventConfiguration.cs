using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.ToTable("PaymentEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StripeEventId).IsRequired().HasMaxLength(120);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(80);
        builder.Property(x => x.ProcessingResult).HasMaxLength(500);
        builder.Property(x => x.RawPayloadHash).IsRequired().HasMaxLength(100);

        builder.Property(x => x.ReceivedAtUtc).IsRequired().HasColumnType("datetime2");
        builder.Property(x => x.ProcessedAtUtc).HasColumnType("datetime2");

        // THE idempotency guard. Stripe retries webhooks; the second insert of the same event
        // violates this index, the handler catches it and returns 200 without re-applying any
        // effect. This single line is what makes payment confirmation provably idempotent.
        builder.HasIndex(x => x.StripeEventId).IsUnique();

        builder.HasOne(x => x.Payment)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
