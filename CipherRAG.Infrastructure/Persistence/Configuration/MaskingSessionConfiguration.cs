using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class MaskingSessionConfiguration : IEntityTypeConfiguration<MaskingSession>
{
    public void Configure(EntityTypeBuilder<MaskingSession> builder)
    {
        builder.ToTable("MaskingSessions");

        // 🔑 Primary Key
        builder.HasKey(x => x.SessionId);
        builder.Property(x => x.SessionId).ValueGeneratedNever();

        builder.Property(x => x.RequestId)
               .IsRequired();

        builder.Property(x => x.ExpiresAt)
               .HasColumnType("datetimeoffset")
               .IsRequired();

        builder.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.MaskedTokens)
               .WithOne(t => t.MaskingSession)
               .HasForeignKey(t => t.SessionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}


///DocmentChunkConfig
