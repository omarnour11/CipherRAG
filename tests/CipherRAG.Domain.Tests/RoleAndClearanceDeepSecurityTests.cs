using CipherRAG.Domain.Entities;
using CipherRAG.Domain.Enums;
using Xunit;

namespace CipherRAG.Domain.Tests;

public class RoleAndClearanceDeepSecurityTests
{
    [Theory]
    // Clearance Level Evaluation Matrix: (userClearance, requiredMin, expectedResult)
    [InlineData(5, 1, true)]
    [InlineData(5, 3, true)]
    [InlineData(5, 5, true)]
    [InlineData(5, 6, false)]
    [InlineData(4, 1, true)]
    [InlineData(4, 4, true)]
    [InlineData(4, 5, false)]
    [InlineData(3, 1, true)]
    [InlineData(3, 2, true)]
    [InlineData(3, 3, true)]
    [InlineData(3, 4, false)]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(null, 0, true)]
    [InlineData(null, 1, false)]
    public void HasClinicalClearance_EvaluatesHierarchyCorrectly(int? userClearance, int requiredMin, bool expectedResult)
    {
        var role = new Role
        {
            RoleName = "TestRole",
            ClinicalClearance = userClearance
        };

        bool hasClearance = role.HasClinicalClearance(requiredMin);
        Assert.Equal(expectedResult, hasClearance);
    }

    [Theory]
    // Clinical Roles
    [InlineData("Physician", RoleCategory.Clinical, true, false)]
    [InlineData("ChiefOfSurgery", RoleCategory.Clinical, true, false)]
    [InlineData("ClinicalNurse", RoleCategory.Clinical, true, false)]
    [InlineData("Cardiologist", RoleCategory.Clinical, true, false)]
    [InlineData("Oncologist", RoleCategory.Clinical, true, false)]
    [InlineData("Radiologist", RoleCategory.Clinical, true, false)]
    [InlineData("Pathologist", RoleCategory.Clinical, true, false)]
    [InlineData("MedicalResident", RoleCategory.Clinical, true, false)]
    [InlineData("ClinicalIntern", RoleCategory.Clinical, true, false)]
    // Operational Roles
    [InlineData("SystemAdmin", RoleCategory.Operational, false, true)]
    [InlineData("AuditOfficer", RoleCategory.Operational, false, true)]
    [InlineData("ComplianceAuditor", RoleCategory.Operational, false, true)]
    [InlineData("SecurityOfficer", RoleCategory.Operational, false, true)]
    [InlineData("OperationsStaff", RoleCategory.Operational, false, true)]
    [InlineData("BillingSpecialist", RoleCategory.Operational, false, true)]
    [InlineData("Receptionist", RoleCategory.Operational, false, true)]
    [InlineData("ITSupport", RoleCategory.Operational, false, true)]
    [InlineData("DataAnalyst", RoleCategory.Operational, false, true)]
    [InlineData("Guest", RoleCategory.Operational, false, true)]
    public void RoleCategory_DistinguishesClinicalFromOperational(
        string roleName, RoleCategory category, bool isClinical, bool isOperational)
    {
        var role = new Role
        {
            RoleName = roleName,
            RoleCategory = category
        };

        Assert.Equal(isOperational, role.IsOperational());
        Assert.Equal(isClinical, role.RoleCategory == RoleCategory.Clinical);
    }

    [Theory]
    // Roles with Policy Management Privileges (Admin only)
    [InlineData("SystemAdmin", true)]
    [InlineData("SecurityOfficer", true)]
    // Roles Forbidden from Managing Policies
    [InlineData("Physician", false)]
    [InlineData("ClinicalNurse", false)]
    [InlineData("Cardiologist", false)]
    [InlineData("AuditOfficer", false)]
    [InlineData("ComplianceAuditor", false)]
    [InlineData("OperationsStaff", false)]
    [InlineData("BillingSpecialist", false)]
    [InlineData("Receptionist", false)]
    [InlineData("Guest", false)]
    [InlineData("MedicalIntern", false)]
    public void CanManagePolicies_RestrictedToAdministrativeRoles(string roleName, bool canManage)
    {
        var role = new Role
        {
            RoleName = roleName,
            CanManagePolicies = canManage
        };

        Assert.Equal(canManage, role.CanManagePolicies);
    }

    [Theory]
    // Roles with Audit Trail Dashboard Access (Admin + Auditors)
    [InlineData("SystemAdmin", true)]
    [InlineData("AuditOfficer", true)]
    [InlineData("ComplianceAuditor", true)]
    [InlineData("ChiefComplianceOfficer", true)]
    // Roles Forbidden from Viewing Audit Logs
    [InlineData("Physician", false)]
    [InlineData("ClinicalNurse", false)]
    [InlineData("Oncologist", false)]
    [InlineData("OperationsStaff", false)]
    [InlineData("BillingSpecialist", false)]
    [InlineData("Receptionist", false)]
    [InlineData("ITSupport", false)]
    [InlineData("Guest", false)]
    [InlineData("Pharmacist", false)]
    public void CanViewAuditDashboard_EnforcesAuditAndComplianceSeparation(string roleName, bool canViewAudit)
    {
        var role = new Role
        {
            RoleName = roleName,
            CanViewAuditDashboard = canViewAudit
        };

        Assert.Equal(canViewAudit, role.CanViewAuditDashboard);
    }

    [Theory]
    // Sensitivity Level Authorization Matrix
    // Public (Clearance >= 0)
    [InlineData(SensitivityLevel.Public, 0, true)]
    [InlineData(SensitivityLevel.Public, 1, true)]
    [InlineData(SensitivityLevel.Public, 3, true)]
    // Internal (Clearance >= 1)
    [InlineData(SensitivityLevel.Internal, 0, false)]
    [InlineData(SensitivityLevel.Internal, 1, true)]
    [InlineData(SensitivityLevel.Internal, 2, true)]
    // Confidential (Clearance >= 2)
    [InlineData(SensitivityLevel.Confidential, 0, false)]
    [InlineData(SensitivityLevel.Confidential, 1, false)]
    [InlineData(SensitivityLevel.Confidential, 2, true)]
    [InlineData(SensitivityLevel.Confidential, 3, true)]
    // Restricted (Clearance >= 3)
    [InlineData(SensitivityLevel.Restricted, 0, false)]
    [InlineData(SensitivityLevel.Restricted, 1, false)]
    [InlineData(SensitivityLevel.Restricted, 2, false)]
    [InlineData(SensitivityLevel.Restricted, 3, true)]
    [InlineData(SensitivityLevel.Restricted, 4, true)]
    [InlineData(SensitivityLevel.Restricted, 5, true)]
    public void SensitivityLevel_EnforcesSufficientClearance(
        SensitivityLevel documentSensitivity, int userClearance, bool expectedAccess)
    {
        int requiredClearance = documentSensitivity switch
        {
            SensitivityLevel.Public => 0,
            SensitivityLevel.Internal => 1,
            SensitivityLevel.Confidential => 2,
            SensitivityLevel.Restricted => 3,
            _ => 99
        };

        var role = new Role
        {
            ClinicalClearance = userClearance
        };

        bool canAccess = role.HasClinicalClearance(requiredClearance);
        Assert.Equal(expectedAccess, canAccess);
    }
}
