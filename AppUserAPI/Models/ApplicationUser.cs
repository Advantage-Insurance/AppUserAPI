namespace AppUserAPI.Models
{
    public class ApplicationUser
    {
        public int ApplicationID { get; set; }

        public string ApplicationCode { get; set; } = string.Empty;

        public int UserID { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsUserActive { get; set; }

        public bool IsApplicationAccessActive { get; set; }

        public int RoleID { get; set; }

        public string RoleName { get; set; } = string.Empty;
    }
}