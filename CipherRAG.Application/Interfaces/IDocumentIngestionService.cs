using CipherRAG.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace CipherRAG.Application.Interfaces;

public interface IDocumentIngestionService
{
    Task<DocumentIngestionResponse> IngestFileAsync(
        IFormFile file,
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentIngestionResponse> IngestExistingDocumentAsync(
        Guid documentId,
        string rawText,
        CancellationToken cancellationToken = default);
}
