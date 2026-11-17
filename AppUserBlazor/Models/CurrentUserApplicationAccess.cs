namespace AppUserBlazor.Models
{
    public class CurrentUserApplicationAccess
    {
        public int UserID { get; set; }

        public string UserName { get; set; } = string.Empty;

        public int ApplicationID { get; set; }

        public string? ApplicationCode { get; set; }

        public bool HasAccess { get; set; }

        public bool IsUserActive { get; set; }

        public bool IsApplicationAccessActive { get; set; }

        public int? RoleID { get; set; }

        public string? RoleName { get; set; }
    }
}