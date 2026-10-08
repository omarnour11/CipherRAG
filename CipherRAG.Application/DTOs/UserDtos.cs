using CipherRAG.Domain.Entities;

namespace CipherRAG.Application.DTOs;

public class UserDto
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int ClearanceLevel { get; set; } = 1;

    public static UserDto FromEntity(User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            RoleId = user.RoleId,
            FullName = user.FullName,
            Email = user.Email,
            Department = user.Department,
            IsActive = user.IsActive,
            RoleName = user.Role?.RoleName ?? string.Empty,
            ClearanceLevel = user.Role?.ClinicalClearance ?? 1
        };
    }
}

public class CreateUserRequest
{
    public Guid RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public Guid RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
