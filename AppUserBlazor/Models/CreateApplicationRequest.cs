using System.ComponentModel.DataAnnotations;

namespace AppUserBlazor.Models;

public class CreateApplicationRequest
{
    [Required(ErrorMessage = "Application code is required.")]
    [StringLength(25)]
    public string ApplicationCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Application name is required.")]
    [StringLength(100)]
    public string ApplicationName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }
}

public class CreateApplicationResponse
{
    public int ReturnCode { get; set; }
    public int? ApplicationID { get; set; }
    public string Message { get; set; } = string.Empty;
}