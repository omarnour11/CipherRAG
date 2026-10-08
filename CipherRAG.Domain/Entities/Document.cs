using CipherRAG.Domain.Enums;

namespace CipherRAG.Domain.Entities;

public class Document
{
    public Guid DocumentID {get;set;}
    public Guid UserId { get; set; }
    public string Title {get;set;} = String.Empty;
    public string Department {get;set;} = String.Empty;
    public SensitivityLevel SensitivityLevel {get;set;}
    public string DigitalSignature {get;set;} = String.Empty;
    public ApprovalStatus ApprovalStatus { get; set; }
    public decimal TrustScore { get; set; } = 1.0m;

    // Navigations
    public User? User { get; set; }
    public List<DocumentChunk> Chunks { get; set; } = [];

    // Domain Logic
    public bool IsApproved()
    {
        return ApprovalStatus == ApprovalStatus.Approved;
    }

    public bool IsTrustworthy(decimal threshold = 0.7m)
    {
        return TrustScore >= threshold;
    }

    public decimal RecomputeTrustScore()
    {
        if (Chunks == null || Chunks.Count == 0)
        {
            TrustScore = 1.0m;
            return TrustScore;
        }

        int cleanCount = Chunks.Count(c => c.PoisonStatus == PoisonStatus.Clean);
        int suspiciousCount = Chunks.Count(c => c.PoisonStatus == PoisonStatus.Suspicious);

        decimal score = (cleanCount + (suspiciousCount * 0.5m)) / Chunks.Count;
        TrustScore = Math.Round(score, 2);
        return TrustScore;
    }

    public bool VerifySignature(string pubKey)
    {
        if (string.IsNullOrWhiteSpace(DigitalSignature) || string.IsNullOrWhiteSpace(pubKey))
            return false;

        // TODO: use System.Security.Cryptography.RSA
        // DigitalSignature with Public Key
        return true;
    }
}

