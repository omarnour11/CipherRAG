using Microsoft.EntityFrameworkCore;
using CipherRAG.Application.Interfaces;
using CipherRAG.Application.Responses;
using CipherRAG.Domain.Entities;

namespace CipherRAG.Infrastructure.Persistence.Repositories;

public class AuditLogRepository(AppDbContext context)
    : GenericRepository<AuditLogEntry>(context), IAuditLogRepository
{
    private readonly AppDbContext _ctx = context;

    public async Task<AuditLogEntry?> GetByIdAsync(long id, CancellationToken ct = default)
        => await _ctx.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.LogId == id, ct);

    public async Task<IEnumerable<AuditLogEntry>> GetFlaggedLogsAsync()
        => await _ctx.AuditLogs
            .AsNoTracking()
            .Where(l => l.InjectionDetected || l.PoisonDetected)
            .OrderByDescending(l => l.LogId)
            .ToListAsync();

    public new async Task<PagedResultResponse<AuditLogEntry>> GetAllAsyncPaged(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 15;

        var total = await _ctx.AuditLogs.CountAsync();
        var items = await _ctx.AuditLogs
            .AsNoTracking()
            .OrderByDescending(l => l.LogId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PagedResultResponse<AuditLogEntry>(items, total, totalPages, pageNumber, pageSize);
    }
}
