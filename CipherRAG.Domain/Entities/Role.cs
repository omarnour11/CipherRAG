using CipherRAG.Domain.Enums;

namespace CipherRAG.Domain.Entities;



public class Role
{
    public Guid RoleId {get; set;}
    public string RoleName {get; set;} = String.Empty;

    public RoleCategory RoleCategory {get;set;}

    public int? ClinicalClearance{get;set;}
    //nav
    public List<AccessPolicy> AccessPolicies { get; set; } = [];

    public bool CanManagePolicies { get; set; }
    public bool CanViewAuditDashboard { get; set; }
    public bool HasClinicalClearance(int min)
    {
        return (ClinicalClearance ?? 0) >= min ;
    }


    public List<User> Users { get; set; } = [];
    public bool IsOperational()
    {
        return (RoleCategory == RoleCategory.Operational);
    }
}