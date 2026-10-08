using CipherRAG.Application.Interfaces;
using CipherRAG.Application.DTOs;
using CipherRAG.Domain.Entities;
using CipherRAG.Domain.Enums;
using Microsoft.AspNetCore.Http;


namespace CipherRAG.Infrastructure.Services;

public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly IVectorEmbeddingService _embeddingService;
    private readonly ICacheService _cacheService;

    public DocumentIngestionService(
        IUnitOfWork unitOfWork,
        IDocumentTextExtractor textExtractor,
        IVectorEmbeddingService embeddingService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _textExtractor = textExtractor;
        _embeddingService = embeddingService;
        _cacheService = cacheService;
    }

    public async Task<DocumentIngestionResponse> IngestFileAsync(
        IFormFile file,
        DocumentUploadRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        // 1. Extract text from uploaded file
        string extractedText = await _textExtractor.ExtractTextAsync(file, ct);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            extractedText = $"[Empty file content for file {file.FileName}]";
        }

        // 2. Resolve valid UserId for Document entity
        Guid ownerId = request.UserId;
        if (ownerId != Guid.Empty)
        {
            var userExists = await _unitOfWork.Users.GetByIdAsync(ownerId);
            if (userExists == null)
            {
                var defaultUser = (await _unitOfWork.Users.GetAllAsync()).FirstOrDefault(u => u.IsActive);
                if (defaultUser != null)
                {
                    ownerId = defaultUser.UserId;
                }
            }
        }
        else
        {
            var defaultUser = (await _unitOfWork.Users.GetAllAsync()).FirstOrDefault(u => u.IsActive);
            if (defaultUser != null)
            {
                ownerId = defaultUser.UserId;
            }
        }

        var document = new Document
        {
            DocumentID = Guid.NewGuid(),
            UserId = ownerId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? file.FileName : request.Title,
            Department = string.IsNullOrWhiteSpace(request.Department) ? "General" : request.Department,
            SensitivityLevel = request.SensitivityLevel,
            DigitalSignature = request.DigitalSignature,
            ApprovalStatus = ApprovalStatus.Approved,
            TrustScore = 1.0m
        };

        // 3. Chunk text and create DocumentChunk entities
        var rawChunks = _embeddingService.ChunkText(extractedText);
        var chunkEntities = new List<DocumentChunk>();

        int clearance = MapSensitivityToClearance(request.SensitivityLevel);

        foreach (var chunkText in rawChunks)
        {
            bool isSuspicious = DetectPoisoningOrInjection(chunkText);
            var embedding = _embeddingService.GenerateEmbedding(chunkText);

            var chunk = new DocumentChunk
            {
                ChunkId = Guid.NewGuid(),
                DocumentId = document.DocumentID,
                VectorPointId = Guid.NewGuid(),
                ClearanceLevel = clearance,
                PoisonStatus = isSuspicious ? PoisonStatus.Suspicious : PoisonStatus.Clean,
                Content = chunkText,
                EmbeddingJson = System.Text.Json.JsonSerializer.Serialize(embedding)
            };

            chunkEntities.Add(chunk);
        }

        document.Chunks = chunkEntities;
        document.RecomputeTrustScore();

        // 4. Save to database
        await _unitOfWork.Documents.AddAsync(document);
        await _unitOfWork.CompleteAsync();

        // 5. Invalidate cache
        await _cacheService.RemoveByPrefixAsync("documents", ct);

        // 7. Map to response
        return MapToIngestionResponse(document, chunkEntities);
    }

    public async Task<DocumentIngestionResponse> IngestExistingDocumentAsync(
        Guid documentId,
        string rawText,
        CancellationToken ct = default)
    {
        var document = await _unitOfWork.Documents.GetDocumentWithChunksAsync(documentId);
        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} was not found.");
        }

        if (string.IsNullOrWhiteSpace(rawText))
        {
            rawText = document.Title;
        }

        var rawChunks = _embeddingService.ChunkText(rawText);
        var chunkEntities = new List<DocumentChunk>();
        int clearance = MapSensitivityToClearance(document.SensitivityLevel);

        foreach (var chunkText in rawChunks)
        {
            bool isSuspicious = DetectPoisoningOrInjection(chunkText);
            var embedding = _embeddingService.GenerateEmbedding(chunkText);

            var chunk = new DocumentChunk
            {
                ChunkId = Guid.NewGuid(),
                DocumentId = document.DocumentID,
                VectorPointId = Guid.NewGuid(),
                ClearanceLevel = clearance,
                PoisonStatus = isSuspicious ? PoisonStatus.Suspicious : PoisonStatus.Clean,
                Content = chunkText,
                EmbeddingJson = System.Text.Json.JsonSerializer.Serialize(embedding)
            };

            chunkEntities.Add(chunk);
        }

        document.Chunks.AddRange(chunkEntities);
        document.RecomputeTrustScore();

        _unitOfWork.Documents.Update(document);
        await _unitOfWork.CompleteAsync();

        await _cacheService.RemoveByPrefixAsync("documents", ct);

        return MapToIngestionResponse(document, chunkEntities);
    }

    private static int MapSensitivityToClearance(SensitivityLevel level)
    {
        return level switch
        {
            SensitivityLevel.Public => 1,
            SensitivityLevel.Internal => 2,
            SensitivityLevel.Confidential => 3,
            SensitivityLevel.Restricted => 4,
            _ => 1
        };
    }

    private static bool DetectPoisoningOrInjection(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        return lower.Contains("ignore previous instructions") ||
               lower.Contains("system override") ||
               lower.Contains("<script>") ||
               lower.Contains("drop table") ||
               lower.Contains("poison_payload");
    }

    private static DocumentIngestionResponse MapToIngestionResponse(Document document, List<DocumentChunk> chunks)
    {
        return new DocumentIngestionResponse
        {
            DocumentId = document.DocumentID,
            Title = document.Title,
            Department = document.Department,
            SensitivityLevel = document.SensitivityLevel,
            ApprovalStatus = document.ApprovalStatus,
            TrustScore = document.TrustScore,
            TotalChunksCreated = chunks.Count,
            Chunks = chunks.Select(c => new DocumentChunkDto
            {
                ChunkId = c.ChunkId,
                VectorPointId = c.VectorPointId,
                ContentSnippet = c.Content.Length > 100 ? c.Content[..100] + "..." : c.Content,
                ClearanceLevel = c.ClearanceLevel,
                PoisonStatus = c.PoisonStatus
            }).ToList()
        };
    }
}
