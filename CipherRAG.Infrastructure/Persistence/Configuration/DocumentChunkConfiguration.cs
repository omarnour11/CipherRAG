using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {

        builder.HasKey(x=> x.ChunkId);
        builder.Property(x=>x.ChunkId).ValueGeneratedNever();
        
        builder.Property(x=>x.DocumentId)
               .IsRequired();

        builder.Property(x => x.VectorPointId)
               .IsRequired();

        builder.Property(x => x.ClearanceLevel)
               .IsRequired();   

        builder.Property(x => x.PoisonStatus)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.Content)
               .IsRequired()
               .HasDefaultValue("");

        builder.Property(x => x.EmbeddingJson)
               .IsRequired(false);

        builder.ToTable("DocumentChunks");



    }

}


///DocmentChunkConfig
