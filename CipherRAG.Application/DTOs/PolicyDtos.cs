using CipherRAG.Domain.Entities;
using CipherRAG.Domain.ValueObjects;

namespace CipherRAG.Application.DTOs;

public class AccessPolicyDto
{
    public Guid PolicyId { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string AllowedDepartment { get; set; } = string.Empty;
    public List<string> AllowedIpRanges { get; set; } = [];
    public List<string> AllowedCountries { get; set; } = [];
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool IsActive { get; set; }

    public static AccessPolicyDto FromEntity(AccessPolicy policy)
    {
        return new AccessPolicyDto
        {
            PolicyId = policy.PolicyId,
            RoleId = policy.RoleId,
            RoleName = policy.Role?.RoleName ?? "General Role",
            AllowedDepartment = policy.AllowedDepartment,
            AllowedIpRanges = policy.AllowedIpRanges ?? [],
            AllowedCountries = policy.AllowedCountries ?? [],
            StartTime = policy.TimeWindow != null ? $"{policy.TimeWindow.StartTime.Hours:D2}:{policy.TimeWindow.StartTime.Minutes:D2}" : null,
            EndTime = policy.TimeWindow != null ? $"{policy.TimeWindow.EndTime.Hours:D2}:{policy.TimeWindow.EndTime.Minutes:D2}" : null,
            IsActive = policy.IsActive
        };
    }
}

public class UpsertAccessPolicyRequest
{
    public Guid RoleId { get; set; }
    public string AllowedDepartment { get; set; } = string.Empty;
    public List<string> AllowedIpRanges { get; set; } = [];
    public List<string> AllowedCountries { get; set; } = [];
    public string? StartTime { get; set; } // e.g. "08:00"
    public string? EndTime { get; set; }   // e.g. "17:00"
    public bool IsActive { get; set; } = true;
}
