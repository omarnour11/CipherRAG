using CipherRAG.API.Filters;
using CipherRAG.Application.Interfaces;
using CipherRAG.Application.DTOs;
using CipherRAG.Application.Responses;
using CipherRAG.Domain.Entities;
using CipherRAG.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CipherRAG.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents")
                       .WithTags("Documents")
                       .AddEndpointFilter<TrackPerformanceFilter>()
                       .RequireAuthorization();

        group.MapGet("/", GetApprovedDocuments)
             .WithName("GetApprovedDocuments");

        group.MapGet("/paged", GetPagedDocuments)
             .WithName("GetPagedDocuments");

        group.MapGet("/{id:guid}", GetDocumentById)
             .WithName("GetDocumentById");

        group.MapPost("/", CreateDocument)
             .WithName("CreateDocument")
             .RequireAuthorization("DocumentManagePolicy");

        group.MapPost("/upload", UploadAndIngestDocument)
             .WithName("UploadAndIngestDocument")
             .RequireAuthorization("DocumentManagePolicy")
             .Accepts<DocumentUploadForm>("multipart/form-data")
             .DisableAntiforgery();

        group.MapPost("/{id:guid}/ingest", IngestDocumentById)
             .WithName("IngestDocumentById")
             .RequireAuthorization("DocumentManagePolicy");

        group.MapPut("/{id:guid}", UpdateDocument)
             .WithName("UpdateDocument")
             .RequireAuthorization("DocumentManagePolicy");

        group.MapDelete("/{id:guid}", DeleteDocument)
             .WithName("DeleteDocument")
             .RequireAuthorization("AdminPolicy");
    }

    // ==========================================
    // Handler Method Definitions
    // ==========================================

    public static async Task<IResult> GetApprovedDocuments(IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<Document> logger, CancellationToken ct)
    {
        logger.LogInformation("Approved documents requested.");
        var dtos = await cacheService.GetOrCreateAsync("documents:approved", async _ =>
        {
            var docs = await unitOfWork.Documents.GetApprovedDocumentsAsync();
            return docs.Select(DocumentDto.FromEntity).ToList();
        }, TimeSpan.FromMinutes(5), ct);

        return Results.Ok(dtos);
    }

    public static async Task<IResult> GetPagedDocuments(int pageNumber, int pageSize, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<Document> logger, CancellationToken ct)
    {
        logger.LogInformation("Paged documents requested. PageNumber={PageNumber}, PageSize={PageSize}", pageNumber, pageSize);
        var response = await cacheService.GetOrCreateAsync($"documents:paged:{pageNumber}:{pageSize}", async _ =>
        {
            var pagedDocs = await unitOfWork.Documents.GetPagedDocumentsWithChunksAsync(pageNumber, pageSize);
            var dtos = pagedDocs.Items.Select(DocumentDto.FromEntity);
            return new PagedResultResponse<DocumentDto>(
                dtos, pagedDocs.TotalCount, pagedDocs.TotalPages, pagedDocs.PageNumber, pagedDocs.PageSize);
        }, TimeSpan.FromMinutes(1), ct);

        return Results.Ok(response);
    }

    public static async Task<IResult> GetDocumentById(Guid id, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<Document> logger, CancellationToken ct)
    {
        logger.LogInformation("Document requested. DocumentId={DocumentId}", id);
        var dto = await cacheService.GetOrCreateAsync($"documents:{id}", async _ =>
        {
            var doc = await unitOfWork.Documents.GetDocumentWithChunksAsync(id);
            return doc is null ? null : DocumentDto.FromEntity(doc);
        }, TimeSpan.FromMinutes(5), ct);

        if (dto is null)
            return Results.NotFound(new { Message = $"Document with ID {id} was not found." });

        return Results.Ok(dto);
    }

    public static async Task<IResult> CreateDocument(CreateDocumentRequest req, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<Document> logger, CancellationToken ct)
    {
        Guid ownerId = req.UserId;
        if (ownerId != Guid.Empty)
        {
            var user = await unitOfWork.Users.GetByIdAsync(ownerId);
            if (user == null)
            {
                var defaultUser = (await unitOfWork.Users.GetAllAsync()).FirstOrDefault(u => u.IsActive);
                if (defaultUser != null)
                {
                    ownerId = defaultUser.UserId;
                }
            }
        }
        else
        {
            var defaultUser = (await unitOfWork.Users.GetAllAsync()).FirstOrDefault(u => u.IsActive);
            if (defaultUser != null)
            {
                ownerId = defaultUser.UserId;
            }
        }

        var doc = new Document
        {
            DocumentID = Guid.NewGuid(),
            UserId = ownerId,
            Title = req.Title,
            Department = req.Department,
            SensitivityLevel = req.SensitivityLevel,
            DigitalSignature = req.DigitalSignature,
            ApprovalStatus = req.ApprovalStatus
        };

        await unitOfWork.Documents.AddAsync(doc);
        await unitOfWork.CompleteAsync();

        await cacheService.RemoveByPrefixAsync("documents", ct);
        logger.LogInformation("Document created. DocumentId={DocumentId}, OwnerId={OwnerId}, Department={Department}, ApprovalStatus={ApprovalStatus}", doc.DocumentID, doc.UserId, doc.Department, doc.ApprovalStatus);

        return Results.Created($"/api/documents/{doc.DocumentID}", DocumentDto.FromEntity(doc));
    }

    public static async Task<IResult> UploadAndIngestDocument(
        [FromForm] DocumentUploadForm form,
        [FromServices] IDocumentIngestionService ingestionService,
        [FromServices] ICacheService cacheService,
        ILogger<Document> logger,
        CancellationToken ct)
    {
        if (form.File == null || form.File.Length == 0)
        {
            return Results.BadRequest(new { Message = "A valid non-empty file (PDF, Word, or TXT) must be uploaded." });
        }

        logger.LogInformation("Document upload and ingest requested. FileName={FileName}, FileSize={FileSize}", form.File.FileName, form.File.Length);

        var uploadReq = new DocumentUploadRequest
        {
            UserId = form.UserId ?? Guid.Empty,
            Title = form.Title ?? form.File.FileName,
            Department = form.Department ?? "General",
            SensitivityLevel = form.SensitivityLevel ?? SensitivityLevel.Internal,
            DigitalSignature = form.DigitalSignature ?? string.Empty
        };

        var result = await ingestionService.IngestFileAsync(form.File, uploadReq, ct);
        await cacheService.RemoveByPrefixAsync("documents", ct);
        logger.LogInformation("Document upload and ingest completed. DocumentId={DocumentId}", result.DocumentId);
        return Results.Created($"/api/documents/{result.DocumentId}", result);
    }

    public static async Task<IResult> IngestDocumentById(
        Guid id,
        [FromBody] IngestRawTextRequest? body,
        [FromServices] IDocumentIngestionService ingestionService,
        [FromServices] ICacheService cacheService,
        ILogger<Document> logger,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Document ingest requested. DocumentId={DocumentId}", id);
            string text = body?.RawText ?? string.Empty;
            var result = await ingestionService.IngestExistingDocumentAsync(id, text, ct);
            await cacheService.RemoveByPrefixAsync("documents", ct);
            logger.LogInformation("Document ingest completed. DocumentId={DocumentId}", id);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("Document ingest failed. DocumentId={DocumentId}. Reason={Reason}", id, ex.Message);
            return Results.NotFound(new { Message = ex.Message });
        }
    }

    public static async Task<IResult> UpdateDocument(Guid id, UpdateDocumentRequest req, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<Document> logger, CancellationToken ct)
    {
        var doc = await unitOfWork.Documents.GetByIdAsync(id);
        if (doc is null)
            return Results.NotFound(new { Message = $"Document with ID {id} was not found." });

        doc.Title = req.Title;
        doc.Department = req.Department;
        doc.SensitivityLevel = req.SensitivityLevel;
        doc.DigitalSignature = req.DigitalSignature;
        doc.ApprovalStatus = req.ApprovalStatus;

        unitOfWork.Documents.Update(doc);
        await unitOfWork.CompleteAsync();

        await cacheService.RemoveByPrefixAsync("documents", ct);
        logger.LogInformation("Document updated. DocumentId={DocumentId}, Department={Department}, ApprovalStatus={ApprovalStatus}", id, doc.Department, doc.ApprovalStatus);

        return Results.Ok(DocumentDto.FromEntity(doc));
    }

    public static async Task<IResult> DeleteDocument(Guid id, IUnitOfWork unitOfWork, ICacheService cacheService, ILogger<Document> logger, CancellationToken ct)
    {
        var doc = await unitOfWork.Documents.GetByIdAsync(id);
        if (doc is null)
            return Results.NotFound(new { Message = $"Document with ID {id} was not found." });

        unitOfWork.Documents.Delete(doc);
        await unitOfWork.CompleteAsync();

        await cacheService.RemoveByPrefixAsync("documents", ct);
        logger.LogInformation("Document deleted. DocumentId={DocumentId}", id);

        return Results.NoContent();
    }
}
