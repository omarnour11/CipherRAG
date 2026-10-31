using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();

        builder.Property(x => x.FullName)
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(x => x.Email)
               .HasMaxLength(255)
               .IsRequired();

        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.Department)
               .HasMaxLength(100);

        builder.HasMany(x => x.RefreshTokens)
               .WithOne(u => u.User)
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Documents)
               .WithOne(u => u.User)
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
