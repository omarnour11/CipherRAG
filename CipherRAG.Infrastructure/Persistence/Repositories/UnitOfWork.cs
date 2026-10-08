using CipherRAG.Application.Interfaces;

namespace CipherRAG.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public IUserRepository Users { get; }
    public IDocumentRepository Documents { get; }
    public IAuditLogRepository AuditLogs { get; }
    public IRoleRepository Roles { get; }

    public UnitOfWork(
        AppDbContext context,
        IUserRepository users,
        IDocumentRepository documents,
        IAuditLogRepository auditLogs,
        IRoleRepository roles)
    {
        _context   = context;
        Users      = users;
        Documents  = documents;
        AuditLogs  = auditLogs;
        Roles      = roles;
    }

    public async Task<int> CompleteAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _context.Dispose();
    }
}