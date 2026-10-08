namespace CipherRAG.Application.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        IDocumentRepository Documents { get; }
        IAuditLogRepository AuditLogs { get; }
        IRoleRepository Roles { get; }

        Task<int> CompleteAsync(CancellationToken ct = default);
    }
}