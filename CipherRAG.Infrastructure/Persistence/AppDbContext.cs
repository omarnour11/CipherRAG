using CipherRAG.Application.Interfaces;
using CipherRAG.Domain.ValueObjects;
using CipherRAG.Domain.Entities;
using CipherRAG.Infrastructure.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;

namespace CipherRAG.Infrastructure.Persistence;



public class AppDbContext : DbContext, IAppDbContext
{

    public AppDbContext(DbContextOptions<AppDbContext> options):base(options)
    {
        
    }
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AccessPolicy> AccessPolicies => Set<AccessPolicy>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<MaskingSession> MaskingSessions => Set<MaskingSession>();
    public DbSet<MaskedToken> MaskedTokens => Set<MaskedToken>();
    public DbSet<RagRequest> RagRequests => Set<RagRequest>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserConfiguration).Assembly));

        // Prevent SQL Server Error 1785 (multiple cascade paths / FK cycles)
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}