using CipherRAG.Domain.Enums;

namespace CipherRAG.Application.DTOs;

public record RagAskRequest
{
    public string UserPrompt { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public Guid UserId { get; init; } = Guid.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public string? Department { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public int ClearanceLevel { get; init; } = 0;
}

public record RagChunkResultDto
{
    public Guid ChunkId { get; init; }
    public Guid DocumentId { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public double RelevanceScore { get; init; }
    public PoisonStatus PoisonStatus { get; init; }
}

public record RagAskResponse
{
    public Guid RequestId { get; init; }

    /// <summary>
    /// HIPAA §164.312(e) — Raw PII prompt is NEVER sent to the client.
    /// Used only server-side for audit logging.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RawPrompt { get; init; } = string.Empty;

    public string MaskedPrompt { get; init; } = string.Empty;
    public string FinalAnswer { get; init; } = string.Empty;
    public List<RagChunkResultDto> RetrievedChunks { get; init; } = [];
    public RagSecurityMetadata SecurityMetadata { get; init; } = new();
}

public record RagSecurityMetadata
{
    public Guid? SessionId { get; init; }
    public Guid? AssessmentId { get; init; }
    public bool InjectionDetected { get; init; }
    public bool PoisonDetected { get; init; }
    public decimal GroundingScore { get; init; }
    public long ExecutionTimeMs { get; init; }
    public int MaskedTokenCount { get; init; }
    public RiskVerdict RiskVerdict { get; init; }
    public decimal CompositeRiskScore { get; init; }
}

public record DocumentUploadRequest
{
    public Guid UserId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public SensitivityLevel SensitivityLevel { get; init; } = SensitivityLevel.Internal;
    public string DigitalSignature { get; init; } = string.Empty;
}

public record DocumentIngestionResponse
{
    public Guid DocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public SensitivityLevel SensitivityLevel { get; init; }
    public ApprovalStatus ApprovalStatus { get; init; }
    public decimal TrustScore { get; init; }
    public int TotalChunksCreated { get; init; }
    public List<DocumentChunkDto> Chunks { get; init; } = [];
}

public record DocumentChunkDto
{
    public Guid ChunkId { get; init; }
    public Guid VectorPointId { get; init; }
    public string ContentSnippet { get; init; } = string.Empty;
    public int ClearanceLevel { get; init; }
    public PoisonStatus PoisonStatus { get; init; }
}
