using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {

        builder.HasKey(x=> x.UserId);
        builder.Property(x=>x.UserId).ValueGeneratedNever();

        builder.Property(x=> x.FullName)
               .HasColumnType("VARCHAR")
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(x=> x.Email)
                .HasColumnType("VARCHAR")
                .HasMaxLength(255)
                .IsRequired();
        builder.HasIndex(x=>x.Email).IsUnique();



        builder.Property(x=> x.Department)
                .HasColumnType("VARCHAR")
                .HasMaxLength(20);
                
        //relations

        builder.HasMany(x=> x.RefreshTokens)
               .WithOne(u=>u.User)
               .HasForeignKey(x=>x.UserId)
               .OnDelete(DeleteBehavior.Cascade);

       builder.HasMany(x=>x.Documents)
              .WithOne(u=>u.User)
              .HasForeignKey(x=>x.UserId)
              .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("Users");



    }

}
