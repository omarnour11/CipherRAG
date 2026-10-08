using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class RagRequestConfiguration : IEntityTypeConfiguration<RagRequest>
{
    public void Configure(EntityTypeBuilder<RagRequest> builder)
    {
        builder.ToTable("RagRequests");

        builder.HasKey(x => x.RequestId);
        builder.Property(x => x.RequestId).ValueGeneratedNever();

        builder.Property(x => x.MaskedPrompt)
               .IsRequired();

        builder.Property(x => x.MaskedFinalResponse)
               .IsRequired(false);

        builder.Property(x => x.CreatedAt)
               .HasColumnType("datetimeoffset")
               .IsRequired();

        builder.Property(x => x.ExecutionTimeMs)
               .IsRequired();

        builder.Property(x => x.Status)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

        // User 1 -> 0..* RagRequest
        builder.HasOne(x => x.User)
               .WithMany(u => u.RagRequests)
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        // RagRequest 1 -> 0..* MaskingSession
        builder.HasMany(x => x.MaskingSessions)
               .WithOne(m => m.RagRequest)
               .HasForeignKey(m => m.RequestId)
               .OnDelete(DeleteBehavior.Cascade);

        // RagRequest 1 -> 0..1 AuditLogEntry
        builder.HasOne(x => x.AuditLogEntry)
               .WithOne(a => a.RagRequest)
               .HasForeignKey<AuditLogEntry>(a => a.RequestId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
