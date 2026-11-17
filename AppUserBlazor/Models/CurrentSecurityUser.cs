namespace AppUserBlazor.Models
{
    public class CurrentSecurityUser
    {
        public int UserID { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? Email { get; set; }

        public bool IsActive { get; set; }
    }
}