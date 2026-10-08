using CipherRAG.Domain.Entities;
using CipherRAG.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace CipherRAG.Application.DTOs;

public record IngestRawTextRequest(string? RawText);

public class DocumentUploadForm
{
    public IFormFile File { get; set; } = default!;
    public Guid? UserId { get; set; }
    public string? Title { get; set; }
    public string? Department { get; set; }
    public SensitivityLevel? SensitivityLevel { get; set; }
    public string? DigitalSignature { get; set; }
}

public class DocumentDto
{
    public Guid DocumentID { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public SensitivityLevel SensitivityLevel { get; set; }
    public string DigitalSignature { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; }
    public int ChunkCount { get; set; }

    public static DocumentDto FromEntity(Document doc)
    {
        return new DocumentDto
        {
            DocumentID = doc.DocumentID,
            UserId = doc.UserId,
            Title = doc.Title,
            Department = doc.Department,
            SensitivityLevel = doc.SensitivityLevel,
            DigitalSignature = doc.DigitalSignature,
            ApprovalStatus = doc.ApprovalStatus,
            ChunkCount = doc.Chunks?.Count ?? 0
        };
    }
}

public class CreateDocumentRequest
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public SensitivityLevel SensitivityLevel { get; set; }
    public string DigitalSignature { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
}

public class UpdateDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public SensitivityLevel SensitivityLevel { get; set; }
    public string DigitalSignature { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; }
}
