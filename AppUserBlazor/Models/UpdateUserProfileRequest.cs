using System.ComponentModel.DataAnnotations;

namespace AppUserBlazor.Models;

public class UpdateUserProfileRequest
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(255)]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;
}

public class UpdateUserProfileResponse
{
    public int ReturnCode { get; set; }
    public int UserID { get; set; }
    public string Message { get; set; } = string.Empty;
}