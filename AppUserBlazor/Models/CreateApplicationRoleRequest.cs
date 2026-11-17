using System.ComponentModel.DataAnnotations;

namespace AppUserBlazor.Models;

public class CreateApplicationRoleRequest
{
    [Required(ErrorMessage = "Role name is required.")]
    [StringLength(50)]
    public string RoleName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }
}

public class CreateApplicationRoleResponse
{
    public int ReturnCode { get; set; }

    public int? RoleID { get; set; }

    public int ApplicationID { get; set; }

    public string Message { get; set; } = string.Empty;
}