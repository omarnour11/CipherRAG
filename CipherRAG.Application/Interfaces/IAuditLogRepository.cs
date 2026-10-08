using CipherRAG.Application.Responses;
using CipherRAG.Domain.Entities;

namespace CipherRAG.Application.Interfaces;

public interface IAuditLogRepository : IGenericRepository<AuditLogEntry>
{
    /// <summary>Gets a single audit log entry by its long identity key.</summary>
    Task<AuditLogEntry?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IEnumerable<AuditLogEntry>> GetFlaggedLogsAsync();
    new Task<PagedResultResponse<AuditLogEntry>> GetAllAsyncPaged(int pageNumber, int pageSize);
}