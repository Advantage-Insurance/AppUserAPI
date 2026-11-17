using System.ComponentModel.DataAnnotations;

namespace AppUserBlazor.Models;

public class UpdateApplicationRoleDescriptionRequest
{
    [StringLength(255)]
    public string? Description { get; set; }
}

public class UpdateApplicationRoleDescriptionResponse
{
    public int ReturnCode { get; set; }
    public int RoleID { get; set; }
    public int ApplicationID { get; set; }
    public string Message { get; set; } = string.Empty;
}