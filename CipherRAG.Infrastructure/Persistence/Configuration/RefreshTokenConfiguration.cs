using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(x => x.TokenId);
        builder.Property(x => x.TokenId).ValueGeneratedNever();

        builder.Property(x => x.TokenHash)
               .HasColumnType("VARCHAR")
               .HasMaxLength(256)
               .IsRequired();

        builder.Property(x => x.DevicePlatform)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.DeviceId)
               .HasMaxLength(100);

        builder.Property(x => x.ExpiresAt)
               .HasColumnType("datetimeoffset")
               .IsRequired();

        builder.Property(x => x.RevokedAt)
               .HasColumnType("datetimeoffset")
               .IsRequired(false);

        builder.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TokenHash).IsUnique();
    }
}