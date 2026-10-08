using CipherRAG.Domain.Enums;

namespace CipherRAG.Domain.Entities;

public class DocumentChunk
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid VectorPointId { get; set; }
    public int ClearanceLevel { get; set; }
    public PoisonStatus PoisonStatus { get; set; }

    public string Content { get; set; } = string.Empty;
    public string? EmbeddingJson { get; set; }

    //nav
    public Document? Document { get; set; }

    // ✍️ اكتب الميثودز هنا:

public bool IsTrustworthy()
    {
        return PoisonStatus == PoisonStatus.Clean; 
    } 

    public bool VerifyIntegrity(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return false;

       // hash check logic 
        return true; 
    }
       
}