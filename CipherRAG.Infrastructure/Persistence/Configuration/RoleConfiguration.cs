using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {

        builder.HasKey(x=> x.RoleId);
        builder.Property(x=>x.RoleId).ValueGeneratedNever();

        builder.Property(x=> x.RoleName)
               .HasColumnType("VARCHAR")
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(x=> x.RoleCategory)
               .HasConversion<string>()
               .HasMaxLength(50);
                
        builder.Property(x=>x.ClinicalClearance)
               .IsRequired(false);
        //relations

        builder.HasMany(r => r.Users)
                .WithOne(u => u.Role)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x=>x.AccessPolicies)
                .WithOne(r=>r.Role)
                .HasForeignKey(x=>x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

       builder.Property(r => r.CanManagePolicies).HasColumnType("bit");
       builder.Property(r => r.CanManagePolicies).HasColumnType("bit");
       
        builder.ToTable("Roles");



    }

}