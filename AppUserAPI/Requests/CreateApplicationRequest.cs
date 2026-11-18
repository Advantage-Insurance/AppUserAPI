namespace AppUserAPI.Models;

public class CreateApplicationRequest
{
    public string ApplicationCode { get; set; } = string.Empty;

    public string ApplicationName { get; set; } = string.Empty;

    public string? Description { get; set; }
}