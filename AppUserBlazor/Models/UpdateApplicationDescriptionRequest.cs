using System.ComponentModel.DataAnnotations;

namespace AppUserBlazor.Models;

public class UpdateApplicationDescriptionRequest
{
    [StringLength(255)]
    public string? Description { get; set; }
}

public class UpdateApplicationDescriptionResponse
{
    public int ReturnCode { get; set; }
    public int ApplicationID { get; set; }
    public string Message { get; set; } = string.Empty;
}