using CipherRAG.Application.Responses;
using CipherRAG.Domain.Entities;

namespace CipherRAG.Application.Interfaces
{
    public interface IDocumentRepository : IGenericRepository<Document>
    {
        Task<IEnumerable<Document>> GetApprovedDocumentsAsync();

        Task<Document?> GetDocumentWithChunksAsync(Guid documentId);

        Task<PagedResultResponse<Document>> GetPagedDocumentsWithChunksAsync(int pageNumber, int pageSize);
    }
}