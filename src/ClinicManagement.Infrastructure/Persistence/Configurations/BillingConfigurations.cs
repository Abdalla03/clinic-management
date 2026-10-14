using ClinicManagement.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicManagement.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    private const int MaxLength = 128;

    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.InvoiceNumber).HasMaxLength(MaxLength).IsRequired();
        builder.Property(invoice => invoice.Currency).HasMaxLength(8).IsRequired();
        builder.Property(invoice => invoice.TotalAmount).HasPrecision(18, 2);
        builder.Property(invoice => invoice.AmountPaid).HasPrecision(18, 2);
        builder.Property(invoice => invoice.IssuedAtUtc).IsRequired();
        builder.Property(invoice => invoice.CreatedAtUtc).IsRequired();
        builder.Property(invoice => invoice.UpdatedAtUtc).IsRequired();
        builder.Property(invoice => invoice.VoidReason).HasMaxLength(512);

        builder.Ignore(invoice => invoice.OutstandingAmount);

        builder.HasIndex(invoice => invoice.InvoiceNumber).IsUnique().HasDatabaseName("UX_Invoices_InvoiceNumber");

        builder.HasIndex(invoice => new { invoice.PatientId, invoice.IssuedAtUtc })
            .HasDatabaseName("IX_Invoices_Patient_IssuedAt");

        builder.HasIndex(invoice => invoice.Status).HasDatabaseName("IX_Invoices_Status");

        builder.HasOne(invoice => invoice.Patient)
            .WithMany()
            .HasForeignKey(invoice => invoice.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invoice => invoice.Visit)
            .WithMany()
            .HasForeignKey(invoice => invoice.VisitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.HasKey(line => line.Id);

        builder.Property(line => line.Description).HasMaxLength(512).IsRequired();
        builder.Property(line => line.UnitPrice).HasPrecision(18, 2);
        builder.Property(line => line.Quantity).IsRequired();

        builder.Ignore(line => line.LineTotal);

        builder.HasIndex(line => line.InvoiceId).HasDatabaseName("IX_InvoiceLines_Invoice");

        builder.HasOne(line => line.Invoice)
            .WithMany(invoice => invoice.Lines)
            .HasForeignKey(line => line.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(payment => payment.Id);

        // The domain assigns payment ids itself; without this, EF treats the client-set key on a
        // store-generated property as an UPDATE when the payment is discovered through the invoice's
        // collection, and the insert fails with a spurious concurrency conflict.
        builder.Property(payment => payment.Id).ValueGeneratedNever();

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.PaidAtUtc).IsRequired();
        builder.Property(payment => payment.Reference).HasMaxLength(128);

        builder.HasIndex(payment => new { payment.InvoiceId, payment.PaidAtUtc })
            .HasDatabaseName("IX_Payments_Invoice_PaidAt");

        builder.HasIndex(payment => payment.PaidAtUtc).HasDatabaseName("IX_Payments_PaidAt");

        builder.HasOne(payment => payment.Invoice)
            .WithMany(invoice => invoice.Payments)
            .HasForeignKey(payment => payment.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
