using CipherRAG.Domain.Enums;

namespace CipherRAG.Domain.Entities;

public class RagRequest
{
    public Guid RequestId { get; set; }
    public Guid UserId { get; set; }
    public string MaskedPrompt { get; set; } = string.Empty;
    public string? MaskedFinalResponse { get; set; }
    /// <summary>Failure reason recorded when Status = Failed. Used in HIPAA audit trail.</summary>
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long ExecutionTimeMs { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    // Navigation Properties
    public User? User { get; set; }
    public List<MaskingSession> MaskingSessions { get; set; } = [];
    public AuditLogEntry? AuditLogEntry { get; set; }

    public bool IsCompleted() => Status == RequestStatus.Completed;

    public void MarkFailed(string error)
    {
        Status = RequestStatus.Failed;
        FailureReason = error;
    }
}