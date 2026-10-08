using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CipherRAG.Infrastructure.Persistence.Configuration;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {  
       
       builder.ToTable("AuditLogs");
        builder.HasKey(x=> x.LogId);
        builder.Property(x=>x.LogId).ValueGeneratedOnAdd();
        
        builder.Property(x=>x.TsUtc)
               .HasColumnType("datetimeoffset")
               .IsRequired();

        builder.Property(x => x.RawPrompt)
               .IsRequired();

        builder.Property(x => x.MaskedPrompt)
               .IsRequired();

        builder.Property(x => x.FinalAnswer)
               .IsRequired(false);

        builder.Property(x => x.MaskedFinalAnswer)
               .IsRequired(false);

        builder.Property(x => x.ActionStatus)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.RetrievedChunkIds);

        builder.Property(x => x.InjectionDetected)
               .HasColumnType("BIT")
               .IsRequired();
               
        builder.Property(x => x.PoisonDetected)
               .HasColumnType("BIT")
               .IsRequired();

        builder.Property( x=>x.GroundingScore)
               .HasPrecision(5,2);

        builder.Property(x=> x.ActionStatus)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

       builder.Property(x => x.PrevRowHash)
               .HasColumnType("VARCHAR")
               .HasMaxLength(255)
               .IsRequired(); 

       builder.Property(x => x.RowHash)
               .HasColumnType("VARCHAR")
               .HasMaxLength(255)
               .IsRequired(); 

builder.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Role)
               .WithMany()
               .HasForeignKey(x => x.RoleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaskingSession)
               .WithMany()
               .HasForeignKey(x => x.SessionId)
               .OnDelete(DeleteBehavior.SetNull); 

        builder.Property(x => x.RiskAssessmentId).IsRequired(false);
        builder.Property(x => x.SessionId).IsRequired(false);
        builder.Property(x => x.RequestId).IsRequired(false);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TsUtc);
        builder.HasIndex(x => x.RowHash).IsUnique();  
       



    }

}


///DocmentChunkConfig
