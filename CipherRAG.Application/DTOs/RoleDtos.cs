using CipherRAG.Domain.Enums;

namespace CipherRAG.Application.DTOs;

public sealed class RoleDto
{
    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public RoleCategory RoleCategory { get; init; }
    public int? ClinicalClearance { get; init; }
    public bool CanManagePolicies { get; init; }
    public bool CanViewAuditDashboard { get; init; }
    public int UserCount { get; init; }
    public int PolicyCount { get; init; }
}

public class UpsertRoleRequest
{
    public string RoleName { get; set; } = string.Empty;
    public RoleCategory RoleCategory { get; set; }
    public int? ClinicalClearance { get; set; }
    public bool CanManagePolicies { get; set; }
    public bool CanViewAuditDashboard { get; set; }
}
