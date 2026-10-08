using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {

        builder.HasKey(x=> x.DocumentID);
        builder.Property(x=>x.DocumentID).ValueGeneratedNever();

        builder.Property(x=> x.Title)
               .HasColumnType("VARCHAR")
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(x=> x.Department)
                .HasColumnType("VARCHAR")
                .HasMaxLength(255)
                .IsRequired();

        builder.Property(x=> x.DigitalSignature)
                .HasColumnType("VARCHAR")
                .HasMaxLength(255)
                .IsRequired();

        builder.Property(x=>x.SensitivityLevel)
               .HasConversion<string>()
               .HasMaxLength(255);

        builder.Property(x=>x.ApprovalStatus)
               .HasConversion<string>()
               .HasMaxLength(255);

        builder.Property(x => x.TrustScore)
               .HasPrecision(5, 2)
               .HasDefaultValue(1.0m);

        //relations

        builder.HasMany(x=>x.Chunks)
               .WithOne(d=>d.Document)
               .HasForeignKey(x=>x.DocumentId)
               .OnDelete(DeleteBehavior.Cascade);

       
        builder.ToTable("Documents");



    }

}


///DocmentChunkConfig
