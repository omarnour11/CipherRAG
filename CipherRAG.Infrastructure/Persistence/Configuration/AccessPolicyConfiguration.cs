using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class AccessPolicyConfiguration : IEntityTypeConfiguration<AccessPolicy>
{
    public void Configure(EntityTypeBuilder<AccessPolicy> builder)
    {
        builder.ToTable("AccessPolicies");

        builder.HasKey(x => x.PolicyId);
        builder.Property(x => x.PolicyId).ValueGeneratedNever();

        builder.Property(x => x.AllowedDepartment)
               .HasMaxLength(100);

        builder.Property(x => x.AllowedIpRanges);
        builder.Property(x => x.AllowedCountries);

        builder.Property(x => x.IsActive)
               .IsRequired();

        builder.OwnsOne(x => x.TimeWindow);

        builder.HasOne(x => x.Role)
               .WithMany(r => r.AccessPolicies)
               .HasForeignKey(x => x.RoleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
