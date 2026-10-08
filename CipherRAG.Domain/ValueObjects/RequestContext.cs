namespace CipherRAG.Domain.ValueObjects;

public class RequestContext
{
    public string UserDepartment { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTimeOffset RequestTime { get; set; } = DateTimeOffset.UtcNow;
}