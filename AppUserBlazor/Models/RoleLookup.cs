namespace AppUserBlazor.Models
{
    public class RoleLookup
    {
        public int RoleID { get; set; }

        public int ApplicationID { get; set; }

        public string RoleName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }
}