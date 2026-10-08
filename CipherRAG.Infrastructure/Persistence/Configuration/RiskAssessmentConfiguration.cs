using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class RiskAssessmentConfiguration : IEntityTypeConfiguration<RiskAssessment>
{
    public void Configure(EntityTypeBuilder<RiskAssessment> builder)
    {
        builder.ToTable("RiskAssessments");

        // 🔑 Primary Key
        builder.HasKey(x => x.AssessmentId);
        builder.Property(x => x.AssessmentId).ValueGeneratedNever();

        // 📝 Query Hash
        builder.Property(x => x.QueryHash)
               .HasColumnType("VARCHAR")
               .HasMaxLength(128);

        builder.Property(x => x.PatternScore)
               .HasPrecision(5, 2);

        builder.Property(x => x.MlModelScore)
               .HasPrecision(5, 2);

        builder.Property(x => x.LlmJudgeScore)
               .HasPrecision(5, 2);

        builder.Property(x => x.CompositeRiskScore)
               .HasPrecision(5, 2);

        // 🚦 Verdict Enum Conversion
        builder.Property(x => x.Verdict)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();
    }
}


///DocmentChunkConfig
