using Microsoft.EntityFrameworkCore;
using CipherRAG.Domain.Entities;
using CipherRAG.Domain.ValueObjects;

namespace CipherRAG.Application.Interfaces;


public interface IAppDbContext
{
     public DbSet<User> Users {get;}
     public DbSet<Role> Roles {get;}
     public DbSet<AccessPolicy> AccessPolicies {get;}
     public DbSet<Document> Documents {get;}
     public DbSet<DocumentChunk> DocumentChunks {get;}
     public DbSet<RefreshToken> RefreshTokens {get;}
     public DbSet<AuditLogEntry> AuditLogs {get;}
     public DbSet<RiskAssessment> RiskAssessments {get;}
     public DbSet<MaskingSession> MaskingSessions {get;}
     public DbSet<MaskedToken> MaskedTokens {get;}
     public DbSet<RagRequest> RagRequests {get;}


     public Task<int> SaveChangesAsync(CancellationToken ct = default);
}