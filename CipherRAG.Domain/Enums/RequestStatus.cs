namespace CipherRAG.Domain.Enums;

public enum RequestStatus
{
    Pending,
    Masking,
    Retrieving,
    Generating,
    Unmasking,
    Completed,
    Blocked,
    Failed
}
