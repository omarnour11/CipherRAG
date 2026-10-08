using CipherRAG.Application.Interfaces;
using CipherRAG.Application.Responses;
using CipherRAG.Domain.Entities;
using CipherRAG.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CipherRAG.Infrastructure.Persistence.Repositories;

public class DocumentRepository : GenericRepository<Document>, IDocumentRepository
{
    public DocumentRepository(AppDbContext context,IMemoryCache cache) : base(context,cache)
    {
    }

    public async Task<IEnumerable<Document>> GetApprovedDocumentsAsync()
    {
        return await _dbSet
            .Include(d => d.Chunks)
            .Where(d => d.ApprovalStatus == ApprovalStatus.Approved)
            .ToListAsync();
    }

    public async Task<Document?> GetDocumentWithChunksAsync(Guid documentId)
    {
        return await _dbSet
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.DocumentID == documentId);
    }

    public async Task<PagedResultResponse<Document>> GetPagedDocumentsWithChunksAsync(int pageNumber, int pageSize)
    {
        var total = await _dbSet.CountAsync();
        var items = await _dbSet
            .Include(d => d.Chunks)
            .AsNoTracking()
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        int totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResultResponse<Document>(items, total, totalPages, pageNumber, pageSize);
    }
}
