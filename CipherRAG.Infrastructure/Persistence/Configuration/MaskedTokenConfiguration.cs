using CipherRAG.Domain.Entities;
using CipherRAG.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class MaskedTokenConfiguration : IEntityTypeConfiguration<MaskedToken>
{
    public void Configure(EntityTypeBuilder<MaskedToken> builder)
    {
        builder.ToTable("MaskedTokens");

        builder.HasKey(x => x.TokenId);
        builder.Property(x => x.TokenId).ValueGeneratedNever();

        builder.Property(x => x.PlaceHolder)
               .HasColumnType("VARCHAR")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(x => x.EncryptedRealValue)
               .IsRequired();

        builder.Property(x => x.EntityType)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.ExpiresAt)
               .HasColumnType("datetimeoffset")
               .IsRequired();

        builder.Property(x => x.SessionId)
               .IsRequired();
    }
}